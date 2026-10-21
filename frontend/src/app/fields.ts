import { Component, Input, forwardRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BrnButton } from '@spartan-ng/brain/button';
import { Schema, kind, initial, label, nullable } from './schema';
@Component({
  selector: 'app-fields',
  imports: [FormsModule, BrnButton, forwardRef(() => Fields)],
  template: `
    @for (entry of entries(); track entry[0]) {
      @let name = entry[0]; @let field = entry[1]; @let key = prefix + '-' + name;
      @if (kind(field) === 'object') {
        <fieldset>
          <legend>{{ label(name) }}</legend>
          @if (nullable(field) || !schema.required?.includes(name)) {
            <label
              ><input
                type="checkbox"
                [checked]="model[name] != null"
                (change)="toggle(name, field)"
              />
              Include {{ label(name) }}</label
            >
          }
          @if (model[name] != null || (schema.required?.includes(name) && !nullable(field))) {
            <app-fields [schema]="field" [model]="object(name, field)" [prefix]="key" />
          }
        </fieldset>
      } @else if (kind(field) === 'array') {
        <fieldset>
          <legend>{{ label(name) }} {{ schema.required?.includes(name) ? '*' : '' }}</legend>
          @for (item of array(name); track $index; let i = $index) {
            @if (kind(field.items ?? {}) === 'object') {
              <app-fields [schema]="field.items ?? {}" [model]="item" [prefix]="key + '-' + i" />
            } @else {
              <label [for]="key + '-' + i">{{ label(name) }} {{ i + 1 }}</label
              ><input
                [id]="key + '-' + i"
                [name]="key + '-' + i"
                [ngModelOptions]="{ standalone: true }"
                [(ngModel)]="model[name][i]"
                [required]="true"
              />
            }
            <button
              brnButton
              type="button"
              class="quiet"
              (click)="model[name].splice(i, 1)"
              [attr.aria-label]="'Remove ' + label(name) + ' ' + (i + 1)"
            >
              Remove
            </button>
          }
          <button
            brnButton
            type="button"
            class="quiet"
            [disabled]="array(name).length >= (field.maxItems ?? 100)"
            (click)="model[name].push(initial(field.items ?? {}))"
          >
            Add {{ label(name) }}
          </button>
        </fieldset>
      } @else {
        <div class="field">
          <label [for]="key"
            >{{ label(name) }} {{ schema.required?.includes(name) ? '*' : '' }}</label
          >
          @if (field.enum) {
            <select
              [id]="key"
              [name]="key"
              [ngModelOptions]="{ standalone: true }"
              [(ngModel)]="model[name]"
              [required]="(schema.required?.includes(name) ?? false) && !nullable(field)"
            >
              <option value="">Choose…</option>
              @for (value of field.enum; track $index) {
                <option [ngValue]="value">{{ value }}</option>
              }
            </select>
          } @else if (kind(field) === 'boolean') {
            <input
              type="checkbox"
              [id]="key"
              [name]="key"
              [ngModelOptions]="{ standalone: true }"
              [(ngModel)]="model[name]"
            />
          } @else if (kind(field) === 'integer' || kind(field) === 'number') {
            <input
              type="number"
              [id]="key"
              [name]="key"
              [ngModelOptions]="{ standalone: true }"
              [(ngModel)]="model[name]"
              [min]="field.minimum ?? null"
              [max]="field.maximum ?? null"
              [required]="(schema.required?.includes(name) ?? false) && !nullable(field)"
            />
          } @else {
            <textarea
              [id]="key"
              [name]="key"
              [ngModelOptions]="{ standalone: true }"
              [(ngModel)]="model[name]"
              [attr.minlength]="field.minLength ?? null"
              [attr.maxlength]="field.maxLength ?? 100000"
              [required]="(schema.required?.includes(name) ?? false) && !nullable(field)"
              rows="2"
            ></textarea>
          }
        </div>
      }
    }
  `,
})
export class Fields {
  @Input({ required: true }) schema!: Schema;
  @Input({ required: true }) model!: any;
  @Input() prefix = 'field';
  nullable = nullable;
  toggle(name: string, s: Schema) {
    this.model[name] = this.model[name] == null ? initial({ ...s, type: kind(s) }) : null;
  }
  kind = kind;
  initial = initial;
  label = label;
  entries() {
    return Object.entries(this.schema.properties ?? {});
  }
  object(name: string, s: Schema) {
    return this.model[name] ?? (this.model[name] = initial(s));
  }
  array(name: string) {
    return this.model[name] ?? (this.model[name] = []);
  }
}
