const ATTEMPTS = 'sso.attempts'

export const signedIn = () => sessionStorage.removeItem(ATTEMPTS)

export const toPortal = () => window.location.assign('/api/account/portal')

// The server runs the sign-in and keeps the tokens. The browser only ever holds a session cookie scripts can't read.
export const login = (returnTo: string) => {
    const attempts = Number(sessionStorage.getItem(ATTEMPTS)) + 1

    // Still no session after this many trips means the browser refuses the cookie: stop instead of bouncing through the portal forever.
    if (attempts > 3) {
        signedIn()
        toPortal()
        return
    }

    sessionStorage.setItem(ATTEMPTS, String(attempts))
    window.location.assign(`/api/account/login?returnUrl=${encodeURIComponent(returnTo)}`)
}
