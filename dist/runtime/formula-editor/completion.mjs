// Command completion is intentionally armed only by direct keyboard text
// insertion. Mouse caret moves, range selection, paste, undo, and deletion
// must never make the suggestion panel appear on their own.
export function isCommandTypingInput(event) {
  return event?.inputType === 'insertText'
    && typeof event.data === 'string'
    && event.data.length > 0
    && /^[\\A-Za-z]+$/.test(event.data);
}
