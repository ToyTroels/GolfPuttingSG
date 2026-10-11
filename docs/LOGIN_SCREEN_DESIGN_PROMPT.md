# GolfSG welcome and login screen prompt

Design and implement the opening login/signup experience for GolfSG, an existing
.NET MAUI mobile app for recording golf rounds and following personal progress.

Use the app's existing GolfSG identity: a warm cream background, white cards,
deep green accents, rounded corners and clear typography. Keep the screen calm
and easy to use on a phone. Use Danish text throughout.

Show the GolfSG name or existing logo, the heading “Velkommen til GolfSG” and the
short introduction “Dine runder. Din udvikling.” Make “Log ind” the primary action
and “Opret konto” an obvious secondary action. Login uses “Brugernavn” and
“Adgangskode”. Account creation also includes “Gentag adgangskode”. Give every
field a visible label and readable placeholder; maintain contrast in both phone
themes, including focused and disabled fields. Mask password fields.

Provide “Fortsæt uden login” for local, offline play. Keep server configuration
behind a small “Serverindstillinger” link and show connection errors clearly
without blocking offline access. Support the phone keyboard and small screens
without hiding the main actions.

Signed-out users should see this screen first on a fresh launch. Successful login
or account creation opens the main app. A remembered account continues to the
main app immediately using the locally saved session, even while the API is asleep.
Opening the app, returning from the background and saving a server address must not
call the API or database. Connection checks run only when the user selects
“Tjek forbindelse”. Login, signup and explicit connection checks allow for the API
taking about a minute to wake, show a clear waiting message and offer “Annuller”.
Allow two minutes per request, prevent duplicate submissions and keep offline access
available. Returning from the background preserves the active page.
Reuse the existing account service and preserve local history. In Historik,
users can import a GolfSG rounds.json export and then choose Synkroniser to upload
their rounds to their account. Keep synchronization manual.
