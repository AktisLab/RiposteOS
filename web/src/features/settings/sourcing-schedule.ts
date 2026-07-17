export const sourcingSchedulePresets = [
  {
    value: 'hourly',
    label: 'Chaque heure',
    description: 'Au début de chaque heure',
    cron: '0 * * * *',
  },
  {
    value: 'six-hours',
    label: 'Toutes les 6 heures',
    description: 'Quatre fois par jour',
    cron: '0 */6 * * *',
  },
  {
    value: 'morning-evening',
    label: 'Matin et soir',
    description: 'À 06:00 et 18:00 UTC',
    cron: '0 6,18 * * *',
  },
  {
    value: 'daily',
    label: 'Une fois par jour',
    description: 'À 06:00 UTC',
    cron: '0 6 * * *',
  },
  {
    value: 'weekdays',
    label: 'Chaque jour ouvré',
    description: 'Du lundi au vendredi à 06:00 UTC',
    cron: '0 6 * * 1-5',
  },
] as const

export function findSchedulePreset(cron: string) {
  return sourcingSchedulePresets.find((preset) => preset.cron === cron) ?? null
}
