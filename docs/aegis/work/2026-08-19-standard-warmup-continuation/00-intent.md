# Task Intent

- Outcome: idle Agents continue warming an already active non-reservation Target Server before starting fresh targets, until the Target Server's shared attempt window succeeds or expires.
- Scope: Core scheduler selection/timing and the frontend wording that exposes server-level timing.
- Non-goals: reservation behavior, A2S/lobby authority, API shape, migrations, and unrelated frontend redesign.
- Risk: `WarmupAttempt.StartedAt` is persisted per Agent attempt today; continuation must derive and preserve the Target Server's earliest active start time.
