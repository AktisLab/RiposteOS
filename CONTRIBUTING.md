# Contribuer à RiposteOS

Merci de donner du temps à RiposteOS. Le projet aide les équipes à sourcer et
qualifier des appels d'offres sans leur imposer un fournisseur d'hébergement,
d'IA ou de traitement documentaire.

Une bonne contribution est petite, motivée par un besoin observable et facile à
vérifier. Avant de commencer un sujet conséquent, ouvrez une issue ou une
discussion : cela évite de construire deux fois la même chose.

## Avant d'écrire du code

- Recherchez les issues et pull requests existantes.
- Décrivez le problème, l'utilisateur concerné et le résultat attendu. Une
  capture, une réponse API ou un scénario reproductible vaut mieux qu'une
  solution imposée d'emblée.
- Pour une évolution qui modifie durablement l'architecture ou les invariants
  métier, discutez-la avant la pull request.

Les corrections de bugs reproductibles, la documentation, les tests et les
améliorations d'accessibilité sont de très bonnes premières contributions.

## Préparer l'environnement

Prérequis : Docker Compose et GNU Make.

```bash
git clone https://github.com/AktisLab/RiposteOS.git
cd RiposteOS
cp .env.example .env
make dev
```

La stack démarre PostgreSQL, applique les migrations, puis lance l'API, le
worker et l'application web. Les services utiles sont disponibles ici :

| Service | Adresse |
| --- | --- |
| Application | <http://localhost:5173> |
| API et OpenAPI | <http://localhost:8080/docs> |
| Santé | <http://localhost:8080/health/live> |

Utilisez `make dev-ai` seulement lorsque vous avez besoin de Docling Serve.

## Trouver le bon emplacement

RiposteOS est un monolithe modulaire. Une fonctionnalité ne doit pas traverser
les couches sans raison.

| Besoin | Emplacement |
| --- | --- |
| Règle métier, entité, valeur ou état | `src/RiposteOS.Core` |
| Persistance, job, adaptateur de source externe | `src/RiposteOS.Infrastructure` |
| Endpoint HTTP, DTO public, mapping API | `src/RiposteOS.Api` |
| Hébergement des jobs | `src/RiposteOS.Worker` |
| Interface utilisateur | `web` |

L'API et le worker appellent l'infrastructure ; l'infrastructure dépend du
cœur. Le cœur ne dépend ni d'EF Core, ni d'ASP.NET, ni de Hangfire, ni d'un
provider externe.

Pour le sourcing, une nouvelle source implémente l'adaptateur existant et
réutilise le même flux d'import. N'ajoutez pas un endpoint, un job ou un
importer dédié par source.

## Développer et vérifier

Créez une branche à partir de `main`, puis gardez la pull request centrée sur
un seul objectif. Ajoutez le plus petit test qui échoue sans le comportement
attendu.

```bash
make format
make test
make coverage
make check
```

`make check` est le contrôle à exécuter avant une pull request : il vérifie le
format, les tests, la couverture, le lint et les builds. Si votre changement
traverse l'API, le worker, PostgreSQL ou l'interface, vérifiez aussi le parcours
réel dans la stack Docker.

Une modification de modèle persistant nécessite une migration EF Core :

```bash
make migration name=NomExplicite
make migrate
```

Inspectez toujours la migration et le snapshot générés. Ne modifiez pas une
migration déjà partagée : ajoutez une migration corrective.

## Ouvrir une pull request

Une pull request doit expliquer clairement :

1. le problème résolu et le comportement obtenu ;
2. les choix importants ou compromis ;
3. les contrôles réellement exécutés ;
4. les éventuels impacts de migration, configuration ou compatibilité.

Ajoutez des captures ou une courte vidéo pour un changement d'interface.
N'incluez ni secrets, ni données client, ni documents d'appels d'offres non
publics. Répondez aux retours de revue avec le contexte nécessaire ; si une
décision change, mettez aussi à jour la description de la pull request.

## Signaler un problème de sécurité

N'ouvrez pas d'issue publique avec les détails exploitables d'une vulnérabilité
ou des données sensibles. Contactez plutôt un mainteneur du dépôt en privé, en
indiquant l'impact, les versions concernées et une reproduction minimale.

## Licence

En contribuant, vous acceptez que votre contribution soit distribuée sous la
licence [AGPL-3.0](LICENSE).
