/** Small assertions for the Deno tests, so the tests do not need an extra dependency. */

export function assert(condition: unknown, message = "Assertion failed"): asserts condition {
  if (!condition) {
    throw new Error(message)
  }
}

export function assertEquals<T>(actual: T, expected: T): void {
  const actualJson = JSON.stringify(actual, null, 2)
  const expectedJson = JSON.stringify(expected, null, 2)
  if (actualJson !== expectedJson) {
    throw new Error(`Expected:\n${expectedJson}\n\nActual:\n${actualJson}`)
  }
}

export function assertStringIncludes(actual: string, expected: string): void {
  if (!actual.includes(expected)) {
    throw new Error(`Expected to find:\n${expected}\n\nIn:\n${actual}`)
  }
}

export function assertThrows(action: () => unknown, ErrorClass: typeof Error, message: string): void {
  try {
    action()
  } catch (error) {
    if (!(error instanceof ErrorClass)) {
      throw new Error(`Expected a ${ErrorClass.name}, got ${error}`)
    }
    if (!error.message.includes(message)) {
      throw new Error(`Expected the error to include "${message}", got "${error.message}"`)
    }
    return
  }

  throw new Error(`Expected an error that includes "${message}", but nothing was thrown.`)
}
