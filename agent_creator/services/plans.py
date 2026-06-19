from agent_creator.models.subscription import (
    PlanFeatures,
    PlanLimits,
    SubscriptionPlan,
    SubscriptionPlanConfig,
)

PLAN_CATALOG: dict[SubscriptionPlan, SubscriptionPlanConfig] = {
    SubscriptionPlan.FREE: SubscriptionPlanConfig(
        plan=SubscriptionPlan.FREE,
        name="Gratuit",
        description="Découverte — dialogue et blueprint, déploiements limités.",
        limits=PlanLimits(
            max_deployments_per_month=2,
            deployment_base_fee_eur=29.0,
            max_conversations_per_month=10,
            max_team_members=1,
        ),
        features=PlanFeatures(
            blueprint_generation=True,
            priority_support=False,
            multi_department=False,
        ),
        monthly_price_eur=0.0,
    ),
    SubscriptionPlan.PROFESSIONAL: SubscriptionPlanConfig(
        plan=SubscriptionPlan.PROFESSIONAL,
        name="Professionnel",
        description="PME — volume modéré de déploiements et support prioritaire.",
        limits=PlanLimits(
            max_deployments_per_month=15,
            deployment_base_fee_eur=49.0,
            max_conversations_per_month=100,
            max_team_members=10,
        ),
        features=PlanFeatures(
            blueprint_generation=True,
            priority_support=True,
            multi_department=False,
        ),
        monthly_price_eur=149.0,
    ),
    SubscriptionPlan.BUSINESS: SubscriptionPlanConfig(
        plan=SubscriptionPlan.BUSINESS,
        name="Business",
        description="Multi-départements — déploiements fréquents et intégrations avancées.",
        limits=PlanLimits(
            max_deployments_per_month=50,
            deployment_base_fee_eur=39.0,
            max_conversations_per_month=0,
            max_team_members=50,
        ),
        features=PlanFeatures(
            blueprint_generation=True,
            priority_support=True,
            multi_department=True,
            custom_integrations=True,
        ),
        monthly_price_eur=499.0,
    ),
    SubscriptionPlan.ENTERPRISE: SubscriptionPlanConfig(
        plan=SubscriptionPlan.ENTERPRISE,
        name="Entreprise",
        description="Grandes organisations — déploiements illimités et SLA dédié.",
        limits=PlanLimits(
            max_deployments_per_month=0,
            deployment_base_fee_eur=29.0,
            max_conversations_per_month=0,
            max_team_members=0,
        ),
        features=PlanFeatures(
            blueprint_generation=True,
            priority_support=True,
            multi_department=True,
            sso=True,
            custom_integrations=True,
            deployment_override=True,
        ),
        monthly_price_eur=0.0,
    ),
}


def get_plan_config(plan: SubscriptionPlan) -> SubscriptionPlanConfig:
    return PLAN_CATALOG[plan]


def list_plans() -> list[SubscriptionPlanConfig]:
    return list(PLAN_CATALOG.values())
