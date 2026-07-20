# 0006 — Charges de débogage des exécutions IA

## Décision

Chaque exécution IA conserve un journal métier générique et, lorsque l'appel a
été émis, une charge de débogage liée 1–1 dans `ai_execution_payloads`. Cette
charge contient l'entrée et la sortie structurées en `jsonb` ; elle ne contient
jamais de secret de configuration.

Les données brutes ne sont ni envoyées aux journaux applicatifs ni ajoutées aux
attributs de télémétrie. L'instrumentation expose uniquement une `ActivitySource`
native .NET, `RiposteOS.Ai`, avec des métadonnées techniques. Elle est compatible
OpenTelemetry sans imposer de collecteur ou d'exporteur à une instance
self-hosted.

Un job Hangfire supprime les charges de plus de 30 jours par défaut. Les
métadonnées de l'exécution sont conservées afin de garder l'historique et les
liens de corrélation utiles au diagnostic.

## Conséquences

L'écran de journal réservé au débogage peut afficher l'échange exact, y compris
pour de futurs types d'exécution IA. Le contenu de documents reste dans le
stockage objet : pour Docling, la charge d'entrée ne conserve que son descripteur
et son empreinte, pas son binaire.

Une instance qui souhaite exporter la télémétrie peut ajouter son propre
collecteur OpenTelemetry et configurer un exporteur sans modifier le modèle
métier ni la capture des données.
