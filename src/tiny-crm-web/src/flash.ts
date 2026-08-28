import { reactive } from 'vue'

// One-shot success banner across a client-side navigation, standing in for the
// original MVC app's TempData["Message"] (e.g. "Customer added.", "Interaction
// logged."). Set before navigating away, read once by the page that lands.
const state = reactive<{ message: string | null }>({ message: null })

export function setFlash(message: string) {
  state.message = message
}

export function takeFlash(): string | null {
  const message = state.message
  state.message = null
  return message
}
