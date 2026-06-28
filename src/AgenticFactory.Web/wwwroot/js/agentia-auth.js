document.querySelectorAll('.auth-password-toggle').forEach((btn) => {
  btn.addEventListener('click', () => {
    const input = btn.closest('.auth-password-input')?.querySelector('input');
    if (!input) return;
    const show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    btn.setAttribute('aria-label', show ? 'Masquer le mot de passe' : 'Afficher le mot de passe');
  });
});
