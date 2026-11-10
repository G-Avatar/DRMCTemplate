// Runs before the page draws, so a dark-mode user doesn't get a white flash. A file, because this app's Content Security Policy blocks inline scripts.
let theme = null
try { theme = localStorage.getItem('theme') } catch {}
document.documentElement.classList.toggle('dark', theme ? theme === 'dark' : matchMedia('(prefers-color-scheme: dark)').matches)
