COMPOSE := docker compose -f compose.dev.yml

.DEFAULT_GOAL := help

.PHONY: help dev dev-ai up down logs ps config build test check audit format migrate migration db-shell

help:
	@printf '%s\n' \
		'make dev              Lance la stack de développement' \
		'make dev-ai           Lance la stack avec Docling Serve' \
		'make down             Arrête la stack' \
		'make logs             Suit les logs' \
		'make build            Compile backend et frontend' \
		'make test             Lance les tests backend' \
		'make check            Vérifie format, lint, build et tests' \
		'make audit            Audite les dépendances' \
		'make migrate          Applique les migrations EF Core' \
		'make migration name=X Crée une migration EF Core'

dev:
	$(COMPOSE) up --build --remove-orphans

dev-ai:
	$(COMPOSE) --profile ai up --build --remove-orphans

up:
	$(COMPOSE) up --build -d --remove-orphans

down:
	$(COMPOSE) down --remove-orphans

logs:
	$(COMPOSE) logs -f

ps:
	$(COMPOSE) ps

config:
	$(COMPOSE) config --quiet

build:
	dotnet build RiposteOS.slnx
	$(COMPOSE) run --rm --no-deps web sh -lc 'pnpm install --frozen-lockfile && pnpm build'

test:
	dotnet test RiposteOS.slnx

check:
	dotnet format RiposteOS.slnx --verify-no-changes
	dotnet test RiposteOS.slnx
	$(COMPOSE) run --rm --no-deps web sh -lc 'pnpm install --frozen-lockfile && pnpm lint && pnpm build'

audit:
	dotnet list RiposteOS.slnx package --vulnerable --include-transitive
	$(COMPOSE) run --rm --no-deps web sh -lc 'pnpm install --frozen-lockfile && pnpm audit --prod'

format:
	dotnet format RiposteOS.slnx
	$(COMPOSE) run --rm --no-deps web sh -lc 'pnpm install --frozen-lockfile && pnpm format'

migrate:
	$(COMPOSE) run --rm migrate

migration:
	@test -n "$(name)" || (printf '%s\n' 'Usage: make migration name=NomMigration' && exit 1)
	dotnet ef migrations add "$(name)" --project src/RiposteOS.Infrastructure --startup-project src/RiposteOS.Api --output-dir Persistence/Migrations

db-shell:
	$(COMPOSE) exec postgres psql -U "$${POSTGRES_USER:-riposteos}" -d "$${POSTGRES_DB:-riposteos}"
