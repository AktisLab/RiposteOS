# RiposteOS

RiposteOS est le système open source et self-hosted pour industrialiser la
réponse aux appels d'offres : sourcing, qualification, RAG, génération
documentaire et conformité.

Le dépôt contient actuellement le socle technique, sans fonctionnalité métier.

## Démarrage

Prérequis : Docker avec Compose et GNU Make.

```bash
cp .env.example .env
make dev
```

- Application web : <http://localhost:5173>
- API : <http://localhost:8080>
- Documentation OpenAPI : <http://localhost:8080/docs>
- Santé API : <http://localhost:8080/health/live>

La première exécution construit les images, démarre PostgreSQL, applique les
migrations, puis lance l'API, le worker et Vite en mode watch.

Docling Serve est volontairement optionnel car son image est volumineuse :

```bash
make dev-ai
```

Son interface est alors disponible sur <http://localhost:5001>.

## Commandes utiles

```bash
make help
make check
make down
make migration name=NomDeLaMigration
```

## Structure

```text
src/
  RiposteOS.Api/
  RiposteOS.Worker/
  RiposteOS.Core/
  RiposteOS.Infrastructure/
tests/
web/
docs/
```

Voir [l'architecture technique](docs/architecture.md) et les
[décisions d'architecture](docs/adr).

## Licence

RiposteOS est distribué sous licence AGPL-3.0. Les composants tiers sont listés
dans [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
