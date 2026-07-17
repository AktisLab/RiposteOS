<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://shieldcn.dev/header/gradient.svg?title=RiposteOS&subtitle=IA+et+self-hosted+pour+r%C3%A9pondre+aux+appels+d%27offres.&align=left&mode=dark&gradient=172033,e84d3d,135&radius=20&border=true&watermark=false" />
    <img src="https://shieldcn.dev/header/gradient.svg?title=RiposteOS&subtitle=IA+et+self-hosted+pour+r%C3%A9pondre+aux+appels+d%27offres.&align=left&mode=light&gradient=fff0e8,ff8b6a,135&titleColor=172033&subtitleColor=3d4c64&radius=20&border=true&watermark=false" alt="RiposteOS — IA et self-hosted pour répondre aux appels d'offres" />
  </picture>
</p>

<p align="center">
  <a href="https://github.com/AktisLab/RiposteOS">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="https://shieldcn.dev/group/badge/.NET-10-512BD4+badge/React-19-61DAFB+badge/PostgreSQL-17-4169E1+github/license/AktisLab/RiposteOS.svg?variant=secondary&mode=dark" />
      <img src="https://shieldcn.dev/group/badge/.NET-10-512BD4+badge/React-19-61DAFB+badge/PostgreSQL-17-4169E1+github/license/AktisLab/RiposteOS.svg?variant=secondary&mode=light" alt=".NET 10, React 19, PostgreSQL 17 et licence" />
    </picture>
  </a>
  <a href="https://github.com/AktisLab/RiposteOS/actions/workflows/ci.yml">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="https://shieldcn.dev/github/ci/AktisLab/RiposteOS.svg?workflow=ci.yml&amp;branch=main&amp;variant=secondary&amp;mode=dark" />
      <img src="https://shieldcn.dev/github/ci/AktisLab/RiposteOS.svg?workflow=ci.yml&amp;branch=main&amp;variant=secondary&amp;mode=light" alt="Statut de la CI" />
    </picture>
  </a>
  <a href="https://github.com/AktisLab/RiposteOS/actions/workflows/ci.yml">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="https://shieldcn.dev/badge/dynamic/json.svg?url=https%3A%2F%2Fraw.githubusercontent.com%2FAktisLab%2FRiposteOS%2Fbadges%2F.github%2Fbadges%2Fcoverage.json&amp;query=%24.lineRate&amp;label=couverture&amp;variant=secondary&amp;color=e84d3d&amp;mode=dark" />
      <img src="https://shieldcn.dev/badge/dynamic/json.svg?url=https%3A%2F%2Fraw.githubusercontent.com%2FAktisLab%2FRiposteOS%2Fbadges%2F.github%2Fbadges%2Fcoverage.json&amp;query=%24.lineRate&amp;label=couverture&amp;variant=secondary&amp;color=e84d3d&amp;mode=light" alt="Couverture backend" />
    </picture>
  </a>
</p>

RiposteOS est une plateforme open source, self-hosted et alimentée par l'IA
pour répondre aux appels d'offres. Le sourcing est le premier module : la cible
est de relier opportunités, documents, exigences, preuves et réponses dans un
espace de travail traçable, sans rendre obligatoire un fournisseur d'IA ou de
traitement documentaire.

## Ce qui fonctionne aujourd'hui

- Collecte BOAMP, TED et PLACE, avec synchronisation manuelle ou planifiée.
- Recherche par mots-clés, CPV et territoires, puis score de pertinence
  explicable.
- Déduplication des avis et historique des imports, erreurs de mapping et
  révisions de contenu.
- Liste filtrable pour qualifier les opportunités et accéder aux informations
  disponibles sur chaque avis.

## Démarrer en local

Il suffit de Docker Compose et de GNU Make.

```bash
cp .env.example .env
make dev
```

La stack démarre PostgreSQL, applique les migrations, puis lance l'API, le
worker et l'application web.

| Service | Adresse |
| --- | --- |
| Application | <http://localhost:5173> |
| API | <http://localhost:8080> |
| OpenAPI | <http://localhost:8080/docs> |
| Santé | <http://localhost:8080/health/live> |

Pour lancer aussi Docling Serve, nécessaire aux futurs traitements documentaires :

```bash
make dev-ai
```

## Architecture

Un monolithe modulaire .NET 10 : l'API et le worker hébergent les cas d'usage,
le cœur conserve les règles métier, et l'infrastructure porte PostgreSQL,
Hangfire et les adaptateurs de sources. L'interface est une application
React/Vite.

```text
src/
  RiposteOS.Api/             API HTTP et OpenAPI
  RiposteOS.Worker/          Exécution des synchronisations
  RiposteOS.Core/            Modèle et règles métier
  RiposteOS.Infrastructure/  PostgreSQL, Hangfire et sources externes
tests/                       Tests backend
web/                         Application React
```

Les détails sont dans [l'architecture technique](docs/architecture.md) et les
[décisions d'architecture](docs/adr).

## Commandes utiles

| Commande | Rôle |
| --- | --- |
| `make dev` | Lance la stack au premier plan |
| `make up` | Lance la stack en arrière-plan |
| `make check` | Vérifie format, tests, couverture, lint et builds |
| `make audit` | Audite les dépendances |
| `make down` | Arrête la stack |

## Contribuer

Les modalités de contribution sont dans [CONTRIBUTING.md](CONTRIBUTING.md).

<p align="center">
  <a href="https://github.com/AktisLab/RiposteOS/graphs/contributors">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="https://shieldcn.dev/contributors/AktisLab/RiposteOS.svg?limit=12&mode=dark&preset=surface&watermark=false" />
      <img src="https://shieldcn.dev/contributors/AktisLab/RiposteOS.svg?limit=12&mode=light&preset=surface&watermark=false" alt="Contributeurs de RiposteOS" />
    </picture>
  </a>
</p>

## Licence

RiposteOS est distribué sous licence [AGPL-3.0](LICENSE). Les composants tiers
sont listés dans [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
