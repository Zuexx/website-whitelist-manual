export function validateDomain(rawInput: string): { ok: true } | { ok: false; error: string } {
  if (!rawInput || rawInput.trim().length === 0) {
    return { ok: false, error: "網域不能是空白。" };
  }
  const trimmed = rawInput.trim();
  if (trimmed.includes(" ")) {
    return { ok: false, error: "網域不能包含空格。" };
  }
  if (trimmed.includes("://")) {
    return { ok: false, error: "請只填網域，不要包含 http:// 或 https://。" };
  }
  if (!trimmed.includes(".") || trimmed.startsWith(".") || trimmed.endsWith(".")) {
    return { ok: false, error: "請輸入完整網域，例如 example.com。" };
  }
  return { ok: true };
}
