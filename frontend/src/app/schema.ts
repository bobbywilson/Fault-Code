export interface Schema {
  type?: string | string[];
  format?: string;
  enum?: unknown[];
  properties?: Record<string, Schema>;
  items?: Schema;
  required?: string[];
  minimum?: number;
  maximum?: number;
  minLength?: number;
  maxLength?: number;
  minItems?: number;
  maxItems?: number;
  pattern?: string;
  default?: unknown;
}
export function kind(s: Schema): string {
  return (
    (Array.isArray(s.type) ? s.type.find((t) => t !== 'null') : s.type) ??
    (s.properties ? 'object' : 'string')
  );
}
export function initial(s: Schema): any {
  if (s.default !== undefined) return s.default;
  switch (kind(s)) {
    case 'object':
      return Object.fromEntries(
        Object.entries(s.properties ?? {})
          .filter(
            ([k, v]) => s.required?.includes(k) || kind(v) === 'object' || kind(v) === 'array',
          )
          .map(([k, v]) => [k, initial(v)]),
      );
    case 'array':
      return Array.from({ length: s.minItems ?? 0 }, () => initial(s.items ?? {}));
    case 'boolean':
      return false;
    default:
      return '';
  }
}
export function label(s: string): string {
  return s.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (x) => x.toUpperCase());
}
export function validate(s: Schema, v: any): boolean {
  if (v === undefined || v === null || v === '')
    return Array.isArray(s.type) && s.type.includes('null');
  const k = kind(s);
  if (s.enum && !s.enum.includes(v)) return false;
  if (k === 'object')
    return (
      typeof v === 'object' &&
      !Array.isArray(v) &&
      (s.required ?? []).every((x) => v[x] !== undefined && v[x] !== null && v[x] !== '') &&
      Object.entries(s.properties ?? {}).every(([key, c]) =>
        v[key] === undefined || v[key] === ''
          ? !(s.required ?? []).includes(key)
          : validate(c, v[key]),
      )
    );
  if (k === 'array')
    return (
      Array.isArray(v) &&
      v.length >= (s.minItems ?? 0) &&
      v.length <= (s.maxItems ?? 100) &&
      v.every((x) => validate(s.items ?? {}, x))
    );
  if (k === 'boolean') return typeof v === 'boolean';
  if (k === 'integer' || k === 'number')
    return (
      typeof v === 'number' &&
      Number.isFinite(v) &&
      (k !== 'integer' || Number.isInteger(v)) &&
      v >= (s.minimum ?? -Infinity) &&
      v <= (s.maximum ?? Infinity)
    );
  return (
    typeof v === 'string' &&
    v.length >= (s.minLength ?? 0) &&
    v.length <= (s.maxLength ?? 100000) &&
    (!s.pattern || new RegExp(s.pattern).test(v)) &&
    (s.format !== 'uuid' || /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(v))
  );
}
export function clean(s: Schema, v: any): any {
  if (kind(s) === 'object')
    return Object.fromEntries(
      Object.entries(v)
        .filter(([k, v]) => v !== '' || s.required?.includes(k))
        .map(([k, v]) => [k, clean(s.properties?.[k] ?? {}, v)]),
    );
  if (kind(s) === 'array') return v.map((x: any) => clean(s.items ?? {}, x));
  return v;
}
