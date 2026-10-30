# Procedure sample: find the hazards

The Showcase HSE warehouse procedure ("PilloleHSEmagazzino") rebuilt with the 2026.7 procedure manager, with primitives
as models. Not registered in `package.json` yet (workstream G).

- `TasksLogic_Procedure`: Guided, max 5 errors, hints on, 300 s time limit, summary with the timeline, analytics key
  prefix `hazards`.
- Tasks: go to the entrance (Reach Zone), put on the protective equipment (3 Targets, parallel sub-tasks), find the 5
  hazards (5 Targets, parallel sub-tasks; 3 Distractors cost 5 points each), answer the safety questions (Question
  pool: 2 drawn from 4).
- Every task is completed by an element: no task listener, no graph.

Checked by a PlayMode run of the whole scene in a clean project (SDK 2026.7): passed 93/100 with one distractor and one
hint.
