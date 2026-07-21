# 0007 — Capacités IA réutilisables par cas d’usage

## Décision

Les fonctionnalités IA restent des cas d’usage explicites. Elles composent des
capacités métier et le runtime `Microsoft.Extensions.AI`; elles n’implémentent
pas un framework générique d’agents propre à RiposteOS.

Les responsabilités sont séparées ainsi :

- `Consultations/Knowledge` expose la connaissance d’un DCE indépendamment du
  chat, de HTTP et de MCP ;
- `Consultations/Assistant` porte le cycle conversationnel et sa politique de
  réponse ;
- `Ai/Tasks` sélectionne une stratégie de provider selon la tâche et les
  capacités requises ;
- `Ai/Providers` adapte les protocoles externes aux abstractions
  `IChatClient` et `IEmbeddingGenerator` ;
- `Ai/Runtime` compose les décorateurs du pipeline de chat ;
- `Ai/Execution` conserve la traçabilité commune des exécutions.

## Patterns retenus

### Facade

`ConsultationKnowledgeFacade` fournit l’entrée limitée vers la recherche, les
documents, les sections et les passages. Les clients n’accèdent pas directement
aux requêtes EF Core du sous-système documentaire.

Référence : <https://refactoring.guru/design-patterns/facade>

### Adapter

`ConsultationKnowledgeTools` traduit les opérations de la façade en
`AIFunction`. Cette adaptation ne contient ni requête métier ni règle de
persistance, ce qui permet à API et MCP d’utiliser la façade sans passer par le
format des tools IA.

Les factories OpenAI-compatible restent les adaptateurs des protocoles externes
vers les interfaces `Microsoft.Extensions.AI`.

Référence : <https://refactoring.guru/design-patterns/adapter>

### Strategy

Les résolveurs de `Ai/Tasks` choisissent à l’exécution un provider compatible
avec la tâche. Le cas d’usage dépend de `IChatClient` ou
`IEmbeddingGenerator`, jamais du SDK du provider.

Référence : <https://refactoring.guru/design-patterns/strategy>

### Decorator

`AiChatClientPipeline` compose les middlewares officiels de
`Microsoft.Extensions.AI` autour du client résolu. Le tool calling et la
télémétrie restent des préoccupations transverses et ne polluent pas les cas
d’usage.

Référence : <https://refactoring.guru/design-patterns/decorator>

## Conséquences

Un nouveau cas d’usage IA doit définir son prompt versionné, ses tools autorisés,
son format de sortie, ses validations et sa transition métier. Il réutilise les
façades et le runtime existants sans hériter d’une classe de workflow générique.

Les références `P1`, `P2`, etc. sont allouées par `PassageReferenceSet`, puis
résolues vers les `DocumentPassageId` canoniques avant toute persistance.

Les workflows multi-étapes ou un framework d’agents ne seront introduits que
lorsqu’un cas d’usage mesuré exige reprise, checkpoint ou validation humaine
entre plusieurs étapes longues.
