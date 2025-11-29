# ⚽ FIFA Tracker

Aplikacja do śledzenia statystyk meczów FIFA granych ze znajomymi z inteligentnym systemem generowania meczów opartym na czasie aktywności graczy.

## 🎮 Funkcjonalności

### 🎯 Zarządzanie Sesjami
- **Typy gier** - 1v1 i 2v2 z automatycznym generowaniem meczów
- **Smart Match Generation** - algorytm priorytetowy zapewniający sprawiedliwy podział meczów
- **Pause/Resume** - możliwość wstrzymania gracza bez utraty historii
- **Activity Tracking** - precyzyjne śledzenie czasu aktywności każdego gracza
- **Dynamiczne regenerowanie** - mecze dostosowują się do aktualnych priorytetów
- **Middleware tracking** - automatyczna aktualizacja czasu aktywności przy każdej akcji

### 👥 Gracze & Statystyki
- **Zarządzanie użytkownikami** - soft delete z zachowaniem pełnej historii meczów
- **Reactivate** - przywracanie nieaktywnych użytkowników
- **Leaderboard** - dwa tryby punktacji:
  - **Standard Scoring** - 3 punkty za wygraną, 1 za remis
  - **Effectiveness Scoring** - współczynnik wygranych/przegranych
- **Statystyki per sesja** - wyniki, bramki, różnica bramek, czas aktywności

### ⚡ Mecze
- **Tworzenie customowych meczów** - dowolne kombinacje graczy
- **Automatyczne generowanie** - zawsze 5 pending meczów gotowych do gry
- **Uniqueness first** - priorytet unikalnych kombinacji
- **Real-time updates** - natychmiastowa aktualizacja po dodaniu wyniku
- **Smart caching** - optymalizacja wydajności przez cachowanie kombinacji

### 📱 Progressive Web App (PWA)
- **Instalacja na urządzeniach mobilnych** - iOS i Android
- **Offline cache** - działanie bez połączenia z internetem
- **Apple meta tags** - pełne wsparcie dla iOS
- **Service Worker** - cache-first strategy dla assetów
- **Responsive design** - hamburger menu na urządzeniach mobilnych

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
- **.NET 9** - najnowsza wersja LTS
- **Clean Architecture** - separacja warstw Domain/Application/Infrastructure/WebApi
- **CQRS Pattern** - MediatR dla command/query separation
- **EF Core 9** - Code-First migrations z PostgreSQL
- **PostgreSQL 16** - relacyjna baza danych
- **Custom Middleware** - SessionActivityMiddleware dla automatycznego trackingu
- **Attribute-based design** - UpdateSessionActivityAttribute dla deklaratywnego trackingu

### Frontend
- **React 19** - najnowsza wersja z Concurrent Features
- **TypeScript** - type-safe development
- **Vite 6** - ultra-szybki bundler z HMR
- **React Router** - routing po stronie klienta
- **Axios** - HTTP client z interceptorami
- **PWA Support** - Service Worker + Manifest
- **Component-based** - modalne dialogi, toast notifications

### Infrastructure
- **Docker & Docker Compose** - pełna konteneryzacja
- **Multi-stage builds** - zoptymalizowane obrazy produkcyjne
- **nginx** - reverse proxy i serving static files
- **Cloudflare Tunnel** - bezpieczny dostęp zdalny bez otwierania portów

## 📱 Dostęp z telefonu (sieć lokalna)

1. **Znajdź swoje IP**: `ipconfig` (Windows) lub `ifconfig` (Linux/Mac)
2. **Utwórz plik** `frontend/.env`:
   ```
   VITE_API_BASE_URL=http://192.168.1.X:5000/api
   ```
3. **Restart**: `docker-compose up --build -d`
4. **Otwórz w telefonie**: `http://192.168.1.X:3000`
5. **Zainstaluj jako PWA** - użyj opcji "Dodaj do ekranu głównego"

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
ALLOWED_ORIGINS=https://twoja-domena.com,https://app.twoja-domena.com
```

## 🏗️ Architektura

### Backend - Clean Architecture
```
FifaTracker.Domain      → Entities, Interfaces
FifaTracker.Application → CQRS Handlers, Services, DTOs
FifaTracker.Infrastructure → EF Core, Persistence
FifaTracker.WebApi      → Controllers, Middleware, Attributes
```

### Kluczowe komponenty
- **MatchGenerator** - inteligentny system generowania meczów z priorytetami
- **MatchCombinationCache** - cachowanie kombinacji dla wydajności
- **SessionActivityMiddleware** - automatyczny tracking czasu aktywności
- **UpdateSessionActivityAttribute** - deklaratywne oznaczanie endpointów

## 📝 Licencja

MIT
