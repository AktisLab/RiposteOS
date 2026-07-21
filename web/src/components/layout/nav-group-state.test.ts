import assert from 'node:assert/strict'
import test from 'node:test'
import { isNavItemActive } from './nav-group-state.ts'

test('keeps a navigation section active on its nested routes', () => {
  const consultations = { title: 'Consultations', url: '/consultations' }

  assert.equal(isNavItemActive('/consultations', consultations), true)
  assert.equal(
    isNavItemActive(
      '/consultations/consultation-id?conversation=id',
      consultations
    ),
    true
  )
  assert.equal(isNavItemActive('/consultations-old', consultations), false)
  assert.equal(isNavItemActive('/other', consultations), false)
})

test('does not treat every route as a child of the home link', () => {
  const home = { title: 'Accueil', url: '/' }

  assert.equal(isNavItemActive('/', home), true)
  assert.equal(isNavItemActive('/consultations', home), false)
})

test('activates a collapsible group from one of its nested links', () => {
  const settings = {
    title: 'Paramètres',
    items: [
      { title: "Vue d'ensemble", url: '/settings' },
      { title: 'IA', url: '/settings/ai' },
    ],
  }

  assert.equal(isNavItemActive('/settings/ai/providers', settings), true)
})
