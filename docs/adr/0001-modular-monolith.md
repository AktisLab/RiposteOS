# ADR 0001 — Monolithe modulaire

Statut : accepté

RiposteOS utilise un monolithe modulaire avec une API et un worker .NET. Les
modules restent dans un même projet métier au départ. Cette structure conserve
des transactions simples et un déploiement self-hosted léger. Des services
séparés ne seront extraits qu'à partir de contraintes mesurées.
