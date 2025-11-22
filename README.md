# FIFA Tracker

Aplikacja do śledzenia statystyk meczów FIFA granych ze znajomymi z inteligentnym systemem generowania meczów opartym na czasie aktywności graczy.

## 🎮 Funkcjonalności

### Zarządzanie Sesjami
- **Sesje gier** (1v1, 2v2, 2v1) z automatycznym generowaniem meczów
- **Smart Match Generation** - algorytm priorytetowy zapewniający sprawiedliwy podział meczów
- **Pause/Resume** - możliwość wstrzymania gracza bez utraty historii
- **Activity Tracking** - śledzenie czasu aktywności każdego gracza
- **Dynamiczne regenerowanie** - mecze dostosowują się do aktualnych priorytetów

### Gracze & Statystyki
- **Zarządzanie użytkownikami** (soft delete - historia meczów zachowana)
- **Reactivate** - przywracanie nieaktywnych użytkowników
- **Leaderboard** - dwa tryby: Standard Scoring i Effectiveness Scoring
- **Statystyki per sesja** - wyniki, bramki, różnica bramek

### Mecze
- **Tworzenie customowych meczów** - dowolne kombinacje graczy
- **Automatyczne generowanie** - zawsze 5 pending meczów
- **Uniqueness first** - priorytet unikalnych kombinacji
- **Real-time updates** - natychmiastowa aktualizacja po dodaniu wyniku

### Progressive Web App (PWA)
- **Instalacja na urządzeniach mobilnych** (iOS/Android)
- **Offline cache** - działanie bez połączenia z internetem
- **Apple meta tags** - pełne wsparcie dla iOS
- **Service Worker** - cache-first strategy dla assetów

## 🚀 Szybki start

### Lokalne uruchomienie (Docker)

```bash
git clone https://github.com/minuss01/FifaTracker.git
cd FifaTracker
docker-compose up -d
```

- **Frontend:** http://localhost:3000
- **API:** http://localhost:5000
- **Swagger:** http://localhost:5000/swagger

### Produkcja

Zobacz: [QUICK_START_PRODUCTION.md](./QUICK_START_PRODUCTION.md)

## 💻 Stack Technologiczny

### Backend
- **.NET 9** - latest LTS
- **Clean Architecture** - Domain/Application/Infrastructure/WebApi
- **CQRS** - MediatR pattern
- **EF Core 9** - Code-First migrations
- **PostgreSQL 16** - relacyjna baza danych
- **Middleware** - attribute-based cross-cutting concerns

### Frontend
- **React 19** - najnowsza wersja
- **TypeScript** - type-safe development
- **Vite 6** - szybki bundler
- **PWA** - Progressive Web App support
- **Component-based architecture** - reusable components

### Infrastructure
- **Docker & Docker Compose** - containerization
- **Multi-stage builds** - optymalizacja obrazów
- **nginx** - reverse proxy i static files
- **Cloudflare Tunnel** - bezpieczny dostęp zdalny

## 📱 Dostęp z telefonu (sieć lokalna)

```powershell
# Lub ręcznie:
# 1. Znajdź IP: ipconfig
# 2. Utwórz frontend/.env: VITE_API_BASE_URL=http://192.168.1.X:5000/api
# 3. Restart: docker-compose up --build -d
# 4. Otwórz: http://192.168.1.X:3000 na telefonie
```

## 🔧 Zmienne środowiskowe

**Backend (PostgreSQL):**
```env
POSTGRES_HOST=postgres           # localhost lub IP zewnętrznej bazy
POSTGRES_PORT=5432
POSTGRES_DB=fifatracker
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DATA_PATH=/path/to/data # Opcjonalnie: własna ścieżka dla danych
```

**Frontend:**
```env
VITE_API_BASE_URL=http://localhost:5000/api
```

**CORS (produkcja):**
```env
ALLOWED_ORIGINS=https://twoja-domena.com
```
## 📝 Licencja

MIT
