# XIVPath Autopilot (MVP scaffold)

Plugin Dalamud experimental pour piloter un deplacement vers une destination.

## Objectif

- `Go to` manuel (coordonnees monde)
- `Go to` depuis coordonnees carte (conversion auto map -> monde)
- `Go to flag` (lit le drapeau carte puis lance le pathfind)
- auto-mount (`/mount roulette`) au depart si option active
- auto-teleport inter-zone sur `Go to flag` (si une aetheryte de la zone cible est debloquee)
- Selecteur des destinations de quetes actives (ouvrir la carte cible)
- Etat de navigation clair (idle/running/paused/arrived/failed)
- Integration IPC `vnavmesh` (sans farming/automation bouclee)
- Arret immediat manuel (`Stop`)

## Positionnement

Ce plugin est pense comme un assistant de navigation ponctuel.

- declenchement manuel uniquement
- pas de rotation combat / interaction auto / boucle de farm
- stop explicite disponible a tout moment

## Dependance vnavmesh

Oui, `vnavmesh` est requis pour les fonctions d'autopilot.

- ce plugin n'embarque pas de moteur de pathfinding maison
- il pilote `vnavmesh` via IPC
- si `vnavmesh` est absent/inactif, les actions de navigation echouent proprement

## Etat actuel

Ce repo est un squelette technique "MVP scaffold":

- architecture plugin + fenetre config
- machine d'etat de navigation
- client IPC vnavmesh encapsule
- commandes chat:
  - `/xivpathauto` (ouvre la config)
  - `/xivpathauto stop`
  - `/xivpathauto pause`
  - `/xivpathauto resume`
  - `/xivpathauto flag`
  - `/xivpathauto goto <x> <y> <z>`

## Options V2 ajoutees

- Utilisation monture (toggle)
- Preference vol (toggle)
- Anti-stuck basique (distance mini + timeout stuck)

Note: le selecteur de quetes combine les liens map actifs et un fallback sur les quetes acceptees, pour eviter les listes vides.

## GitHub Actions incluses

- `plugin-ci.yml`: build sur `main` / PR
- `build.yml`: build + release automatique sur tag `v*`
- `dalamud-compat.yml`: check hebdomadaire de compat build
