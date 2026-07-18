import assert from 'node:assert/strict'
import test from 'node:test'
import { getApiErrorMessage } from './api-client.ts'

test('reads useful API problem details', async () => {
  const detail = await getApiErrorMessage(
    Response.json({ title: 'Conflit', detail: 'La consultation existe déjà.' }),
    'Erreur générique'
  )
  const validation = await getApiErrorMessage(
    Response.json({ errors: { title: ['Le titre est obligatoire.'] } }),
    'Erreur générique'
  )
  const stringBody = await getApiErrorMessage(
    Response.json("L'opportunité ne peut plus être écartée."),
    'Erreur générique'
  )
  const message = await getApiErrorMessage(
    Response.json({ message: 'Cette opportunité ne peut plus être écartée.' }),
    'Erreur générique'
  )

  assert.equal(detail, 'La consultation existe déjà.')
  assert.equal(validation, 'Le titre est obligatoire.')
  assert.equal(stringBody, "L'opportunité ne peut plus être écartée.")
  assert.equal(message, 'Cette opportunité ne peut plus être écartée.')
})

test('falls back when the response is not exploitable', async () => {
  const response = new Response('<html>Erreur</html>', {
    status: 500,
    headers: { 'content-type': 'text/html' },
  })

  assert.equal(
    await getApiErrorMessage(response, 'Impossible de continuer.'),
    'Impossible de continuer.'
  )
})
