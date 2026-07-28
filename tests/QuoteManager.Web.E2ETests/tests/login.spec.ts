import { expect, test } from '@playwright/test'

const demoPassword = 'Demo!2345'

test('an unauthenticated visit redirects to login, and signing in reaches the dashboard', async ({
  page,
}) => {
  await page.goto('/')

  await expect(page).toHaveURL(/\/login$/)

  await page.getByLabel('Email').fill('reviewer@quotemgr.test')
  await page.getByRole('textbox', { name: 'Password' }).fill(demoPassword)
  await page.getByRole('button', { name: 'Sign in' }).click()

  await expect(page).toHaveURL(/\/dashboard$/)
  await expect(page.getByText('Rae Reviewer')).toBeVisible()
})

test('a wrong password surfaces the server-provided message rather than a generic one', async ({
  page,
}) => {
  await page.goto('/login')

  await page.getByLabel('Email').fill('reviewer@quotemgr.test')
  await page.getByRole('textbox', { name: 'Password' }).fill('not-the-password')
  await page.getByRole('button', { name: 'Sign in' }).click()

  await expect(page.getByText('The email or password is incorrect.')).toBeVisible()
})

test('signing out clears the session and returns to login', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel('Email').fill('reviewer@quotemgr.test')
  await page.getByRole('textbox', { name: 'Password' }).fill(demoPassword)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/dashboard$/)

  await page.getByRole('button', { name: 'Sign out' }).click()

  await expect(page).toHaveURL(/\/login$/)
  const session = await page.evaluate(() => sessionStorage.getItem('qm.session'))
  expect(session).toBeNull()
})
