# ShepherdMeet

**ShepherdMeet** is a web application for scheduling one-on-one meetings with Father Danial at St. Philopater & St. Demiana Coptic Orthodox Church. Parishioners sign in with their Google account, browse available appointment slots on a calendar, and book a time — no back-and-forth required.

---

## Features

- **Google Sign-In** — Secure, one-click authentication via Google OAuth 2.0. No passwords to manage.
- **Appointment Booking** — Browse available time slots on an interactive calendar and select a 30-minute appointment.
- **Admin Panel** — The priest can manage availability: add time ranges, set specific days and hours, and block off unavailable periods.
- **Role-Based Views** — The admin sees the availability management panel; all other users see the booking calendar.
- **Confirmation Page** — Review and confirm appointment details before submitting.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | React 18, React Router v6 |
| Backend | ASP.NET Core (.NET 8) Minimal APIs |
| Database | SQLite + Entity Framework Core 8 |
| Authentication | Google OAuth 2.0 (cookie-based sessions) |
| Containerization | Docker, Docker Compose |
| Hosting | Azure Container Apps (West US 2) |
| Registry | Azure Container Registry |
| CI/CD | GitHub Actions |
| Persistent Storage | Azure Files (mounted at `/data`) |

---

## Project Structure

```
ShepherdMeet/
├── backend/                  # ASP.NET Core API
│   ├── Models/               # EF Core entity models
│   ├── Migrations/           # EF Core database migrations
│   ├── Program.cs            # Minimal API endpoints & middleware
│   ├── AppDbContext.cs       # Database context
│   ├── ShepherdMeet.csproj
│   ├── appsettings.json
│   └── Dockerfile
├── frontend/                 # React application
│   ├── src/
│   │   ├── App.js            # Root component + auth gate + role routing
│   │   ├── login.js          # Google Sign-In screen
│   │   ├── UserForm.js       # Appointment booking calendar
│   │   ├── PriestAvailabilityForm.js  # Admin availability manager
│   │   ├── ConfirmationPage.js
│   │   └── WeeklyAvailabilityForm.js
│   ├── public/
│   ├── package.json
│   ├── nginx.conf            # SPA routing for production
│   └── Dockerfile
├── docker-compose.yml        # Local development setup
└── .github/workflows/
    └── deploy.yml            # CI/CD pipeline
```

---

## Local Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- A Google OAuth Client ID & Secret ([Google Cloud Console](https://console.cloud.google.com))

### Option A — Docker Compose (recommended)

```bash
# Clone the repo
git clone https://github.com/alexmekhail/ShepherdMeet.git
cd ShepherdMeet

# Create .env from the example and fill in your Google credentials
cp .env.example .env

# Start both containers
docker compose up --build
```

- Frontend: `http://localhost:3000`
- Backend: `http://localhost:5209`

### Option B — Run services individually

**Backend:**
```bash
cd backend

# Store Google credentials securely (never committed to git)
dotnet user-secrets set "Authentication:Google:ClientId" "<your-client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<your-client-secret>"

# Apply migrations and start
dotnet ef database update
dotnet run
```

**Frontend:**
```bash
cd frontend
cp .env.example .env          # Set REACT_APP_API_URL=http://localhost:5209
npm install
npm start
```

### Google OAuth redirect URIs (for local dev)

Add these to your Google Cloud Console OAuth client:
- Authorized JavaScript origin: `http://localhost:3000`
- Authorized redirect URI: `http://localhost:5209/signin-google`

---

## Production Deployment

The app is hosted on **Azure Container Apps** and deploys automatically on every push to `main`.

**Live URLs:**
- 🌐 Frontend: `https://shepherdmeet-frontend.agreeablemeadow-f95d9ead.westus2.azurecontainerapps.io`
- ⚙️ Backend: `https://shepherdmeet-backend.agreeablemeadow-f95d9ead.westus2.azurecontainerapps.io`

### Required GitHub Secrets

| Secret | Description |
|---|---|
| `AZURE_CREDENTIALS` | Azure service principal JSON |
| `ACR_LOGIN_SERVER` | `shepherdmeet.azurecr.io` |
| `ACR_NAME` | `shepherdmeet` |
| `ACR_USERNAME` | ACR admin username |
| `ACR_PASSWORD` | ACR admin password |
| `BACKEND_URL` | Public backend URL (used as `REACT_APP_API_URL` at build time) |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID |
| `GOOGLE_CLIENT_SECRET` | Google OAuth client secret |

### CI/CD Pipeline

On every push to `main`, GitHub Actions will:
1. Build and push Docker images to Azure Container Registry
2. Configure ACR pull credentials on both Container Apps
3. Deploy the new backend image (migrations run automatically on startup)
4. Deploy the new frontend image

---

## Database

SQLite is used with Entity Framework Core. Migrations are applied automatically on backend startup via `db.Database.Migrate()`. In production, the SQLite file lives at `/data/shepherdmeet.db` on a persistent Azure Files volume, so data survives container restarts and redeployments.

To create a new migration after model changes:
```bash
cd backend
dotnet ef migrations add <MigrationName>
```

---

## Environment Variables

### Backend

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | SQLite connection string | `Data Source=/data/shepherdmeet.db` |
| `Authentication__Google__ClientId` | Google OAuth client ID | — |
| `Authentication__Google__ClientSecret` | Google OAuth client secret | — |
| `AllowedOrigins` | Comma-separated CORS origins | `http://localhost:3000` |
| `FrontendUrl` | Post-login redirect base URL | `http://localhost:3000` |

### Frontend

| Variable | Description | Default |
|---|---|---|
| `REACT_APP_API_URL` | Backend API base URL | `http://localhost:5209` |
