import { describe, it, expect } from 'vitest';
import { validate, clean, initial } from './schema';
describe('workflow form validation', () => {
  it('rejects invalid required, versioned identifiers, bounds and nested arrays', () => {
    const s = {
      type: 'object',
      properties: {
        id: { type: 'string', format: 'uuid' },
        steps: {
          type: 'array',
          minItems: 1,
          items: {
            type: 'object',
            required: ['title'],
            properties: { title: { type: 'string', minLength: 2 } },
          },
        },
      },
      required: ['id', 'steps'],
    };
    expect(validate(s, initial(s))).toBe(false);
    expect(
      validate(s, { id: '55555555-5555-4555-8555-555555555555', steps: [{ title: 'Inspect' }] }),
    ).toBe(true);
    expect(validate(s, { id: 'bad', steps: [] })).toBe(false);
    expect(
      validate(s, { id: '55555555-5555-4555-8555-555555555555', steps: [{ title: '' }] }),
    ).toBe(false);
  });
  it('preserves false and zero while omitting optional blank fields', () => {
    expect(
      clean({ type: 'object', properties: {} }, { enabled: false, count: 0, note: '' }),
    ).toEqual({ enabled: false, count: 0 });
  });
  it('rejects invalid enums, integers and array bounds', () => {
    expect(validate({ type: 'integer', minimum: 1 }, 1.5)).toBe(false);
    expect(validate({ type: 'string', enum: ['Approved'] }, 'Draft')).toBe(false);
    expect(
      validate({ type: 'array', maxItems: 1, items: { type: 'boolean' } }, [true, false]),
    ).toBe(false);
  });
});

describe('nullable API schema fields', () => {
  it('retains required nullable properties and omits optional nested objects until chosen', () => {
    const s = {
      type: 'object',
      required: ['model', 'patch'],
      properties: {
        model: { type: ['string', 'null'] },
        patch: {
          type: ['object', 'null'],
          required: ['reference'],
          properties: { reference: { type: 'string' } },
        },
        optional: { type: 'object', required: ['name'], properties: { name: { type: 'string' } } },
      },
    };
    expect(initial(s)).toEqual({ model: null, patch: null });
    expect(validate(s, initial(s))).toBe(true);
    expect(validate(s, {})).toBe(false);
    expect(validate(s, { model: null, patch: { reference: '' } })).toBe(false);
    expect(clean(s, { model: '', patch: null })).toEqual({ model: null, patch: null });
  });
});
