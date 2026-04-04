# Frontend Vue

Ce dossier contient un frontend Vue 3 minimal base sur Vite pour consommer l'API ASP.NET Core.

## Installation

1. Installer Node.js.
2. Dans ce dossier, lancer `npm install`.
3. Demarrer le frontend avec `npm run dev`.

## Backend .NET

L'API existante autorise deja les appels depuis `http://localhost:5173`.

Lancer l'API avec :

```powershell
dotnet run --project .\CodexTest\CodexTest.csproj
```

Puis ouvrir le frontend sur `http://localhost:5173`.
