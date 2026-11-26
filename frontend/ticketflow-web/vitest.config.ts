import { defineConfig } from 'vitest/config';

// Loaded by `ng test` (angular.json → test.options.runnerConfig). Angular's builder supplies
// everything else (jsdom, TestBed setup, spec discovery).
export default defineConfig({
  test: {
    // Each worker runs its own jsdom. Two keep the suite fast while staying stable on machines
    // and CI runners with little free memory (one worker per core crashed with ~2 GB free).
    maxWorkers: 2,
  },
});
