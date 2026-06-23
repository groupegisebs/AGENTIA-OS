"""CLI de demonstration pour l'Agent Creator."""

import json
from typing import Optional

import httpx
import typer
from rich.console import Console
from rich.json import JSON
from rich.panel import Panel

app = typer.Typer(help="Agent Creator - demo dialogue -> blueprint -> deploy")
console = Console()

DEFAULT_BASE = "http://localhost:8000"


def _base_url(url: Optional[str]) -> str:
    return (url or DEFAULT_BASE).rstrip("/")


def _print_plans(client: httpx.Client, url: str) -> None:
    console.print("[bold]Plans d'abonnement[/bold]")
    r = client.get(f"{url}/plans")
    r.raise_for_status()
    plans = r.json()
    console.print(Panel(JSON(json.dumps(plans, indent=2, default=str, ensure_ascii=True)), title="GET /plans"))


def _fetch_org_id(client: httpx.Client, url: str) -> str:
    r = client.get(f"{url}/organizations/me")
    r.raise_for_status()
    return r.json()["id"]


@app.command("health")
def health(base_url: Optional[str] = typer.Option(None, "--url", "-u")) -> None:
    """Verifie que le serveur est accessible."""
    response = httpx.get(f"{_base_url(base_url)}/health", timeout=10.0)
    response.raise_for_status()
    console.print(Panel(JSON(response.text), title="Sante du service"))


@app.command("plans")
def plans_cmd(base_url: Optional[str] = typer.Option(None, "--url", "-u")) -> None:
    """Liste les plans d'abonnement."""
    url = _base_url(base_url)
    with httpx.Client(timeout=30.0) as client:
        _print_plans(client, url)


@app.command("demo")
def demo(
    message: str = typer.Option(
        "Chaque fois qu'une facture arrive par email, je veux extraire les donnees, "
        "les enregistrer dans mon systeme comptable et envoyer un rapport quotidien.",
        "--message",
        "-m",
        help="Premier message decrivant le besoin",
    ),
    follow_up: Optional[str] = typer.Option(
        None,
        "--follow-up",
        "-f",
        help="Deuxieme message optionnel pour enrichir le dialogue",
    ),
    base_url: Optional[str] = typer.Option(None, "--url", "-u"),
    skip_deploy: bool = typer.Option(False, "--skip-deploy", help="Arreter apres le blueprint"),
) -> None:
    """Flux complet : plans -> conversation -> blueprint -> deploy -> facturation."""
    url = _base_url(base_url)

    with httpx.Client(timeout=60.0) as client:
        console.print("[bold]1. Plans[/bold]")
        _print_plans(client, url)

        console.print("\n[bold]2. Creation de la conversation[/bold]")
        r1 = client.post(f"{url}/conversations", json={"message": message})
        r1.raise_for_status()
        data1 = r1.json()
        conv_id = data1["conversation"]["id"]
        console.print(Panel(data1["assistant_message"]["content"], title="Agent"))

        step = 3
        if follow_up:
            console.print(f"\n[bold]{step}. Message de suivi[/bold]")
            r2 = client.post(
                f"{url}/conversations/{conv_id}/messages",
                json={"message": follow_up},
            )
            r2.raise_for_status()
            data2 = r2.json()
            console.print(Panel(data2["assistant_message"]["content"], title="Agent"))
            step += 1

        console.print(f"\n[bold]{step}. Generation du blueprint[/bold]")
        r3 = client.get(f"{url}/conversations/{conv_id}/blueprint")
        r3.raise_for_status()
        blueprint_payload = r3.json()
        blueprint = blueprint_payload["blueprint"]
        console.print(
            Panel(
                JSON(json.dumps(blueprint, indent=2, default=str, ensure_ascii=True)),
                title="Blueprint",
            )
        )
        hint = blueprint_payload.get("deployment_hint")
        if hint:
            console.print(f"[cyan]{hint}[/cyan]")
        step += 1

        if skip_deploy:
            console.print(f"\n[green]Conversation ID : {conv_id}[/green]")
            return

        console.print(f"\n[bold]{step}. Deploiement (POST /deploy)[/bold]")
        r4 = client.post(f"{url}/conversations/{conv_id}/deploy")
        r4.raise_for_status()
        deploy_data = r4.json()
        deployment = deploy_data["deployment"]
        cost = deployment["deployment_cost"]
        currency = deployment.get("currency", "EUR")
        console.print(Panel(deploy_data.get("message", ""), title="Deploiement"))
        console.print(
            f"[bold green]Cout de deploiement : {cost:.2f} {currency}[/bold green] "
            f"(statut : {deployment['status']})"
        )
        if deploy_data.get("billing_event"):
            be = deploy_data["billing_event"]
            console.print(
                f"Evenement facturation : {be['amount']:.2f} {be['currency']} — {be['status']}"
            )
        step += 1

        org_id = _fetch_org_id(client, url)
        console.print(f"\n[bold]{step}. Facturation (GET /organizations/.../billing)[/bold]")
        r5 = client.get(f"{url}/organizations/{org_id}/billing")
        r5.raise_for_status()
        billing = r5.json()
        console.print(
            Panel(
                JSON(json.dumps(billing, indent=2, default=str, ensure_ascii=True)),
                title="Resume facturation",
            )
        )
        console.print(
            f"\n[green]Total facture : {billing['total_billed']:.2f} {billing.get('currency', 'EUR')}[/green]"
        )
        console.print(f"[green]Conversation ID : {conv_id}[/green]")


@app.command("chat")
def chat(base_url: Optional[str] = typer.Option(None, "--url", "-u")) -> None:
    """Mode interactif : dialogue puis generation du blueprint."""
    from rich.prompt import Prompt

    url = _base_url(base_url)
    console.print("[bold]Agent Creator — mode interactif[/bold]")
    console.print("Tapez 'quit' pour terminer et generer le blueprint.\n")

    first = Prompt.ask("Decrivez votre besoin metier")
    if first.strip().lower() == "quit":
        raise typer.Exit(0)

    with httpx.Client(timeout=60.0) as client:
        r = client.post(f"{url}/conversations", json={"message": first})
        r.raise_for_status()
        data = r.json()
        conv_id = data["conversation"]["id"]
        console.print(Panel(data["assistant_message"]["content"], title="Agent"))

        while True:
            msg = Prompt.ask("\nVous")
            if msg.strip().lower() == "quit":
                break
            r = client.post(
                f"{url}/conversations/{conv_id}/messages",
                json={"message": msg},
            )
            r.raise_for_status()
            data = r.json()
            console.print(Panel(data["assistant_message"]["content"], title="Agent"))

        console.print("\n[bold]Generation du blueprint...[/bold]")
        r = client.get(f"{url}/conversations/{conv_id}/blueprint")
        r.raise_for_status()
        blueprint = r.json()["blueprint"]
        console.print(Panel(JSON(json.dumps(blueprint, indent=2, default=str, ensure_ascii=True)), title="Blueprint"))


def main() -> None:
    app()


if __name__ == "__main__":
    main()

