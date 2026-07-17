import assert from 'node:assert/strict'
import test from 'node:test'
import { findSchedulePreset } from './sourcing-schedule.ts'

test('maps known schedules and keeps custom cron expressions advanced', () => {
  assert.equal(findSchedulePreset('0 */6 * * *')?.value, 'six-hours')
  assert.equal(findSchedulePreset('15 4 * * 2'), null)
})
