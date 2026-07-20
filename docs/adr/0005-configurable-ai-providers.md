# 0005 — Providers IA configurés par tâche

## Décision

Les providers IA sont configurés par l'utilisateur dans la base, sans secret.
Une éventuelle clé est lue depuis la variable d'environnement référencée par
sa configuration. Chaque tâche IA implémentée est affectée à un provider actif.

Le métier dépend de `IChatClient` de Microsoft.Extensions.AI. Les détails du
protocole OpenAI-compatible restent dans Infrastructure.

## Conséquences

Changer de serveur ou de modèle ne demande pas de redéploiement. Les exécutions
conservent le provider et le modèle utilisés afin de rester traçables. Les
contenus bruts, lorsqu'ils sont nécessaires au débogage, suivent la politique
dédiée de l'ADR 0006 et ne sont jamais écrits dans les journaux applicatifs.
