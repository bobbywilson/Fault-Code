import { Component, Input, forwardRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { Schema, kind, initial, label, nullable } from './schema';
@Component({
  selector: 'app-fields',
  imports: [
    FormsModule,
    HlmButton,
    HlmFieldImports,
    HlmInput,
    HlmTextarea,
    HlmNativeSelectImports,
    HlmCheckbox,
    forwardRef(() => Fields),
  ],
  template: `
    @for (entry of entries(); track entry[0]) {
      @let name = entry[0]; @let field = entry[1]; @let key = prefix + '-' + name;
      @if (kind(field) === 'object') {
        <fieldset hlmFieldSet class="my-4">
          <legend hlmFieldLegend>{{ label(name) }}</legend>
          @if (nullable(field) || !schema.required?.includes(name)) {
            <div hlmField orientation="horizontal">
              <hlm-checkbox
                [aria-label]="'Include ' + label(name)"
                [checked]="model[name] != null"
                (checkedChange)="toggle(name, field)"
              />
              <span hlmFieldLabel>Include {{ label(name) }}</span>
            </div>
          }
          @if (model[name] != null || (schema.required?.includes(name) && !nullable(field))) {
            <app-fields [schema]="field" [model]="object(name, field)" [prefix]="key" />
          }
        </fieldset>
      } @else if (kind(field) === 'array') {
        <fieldset hlmFieldSet class="my-4">
          <legend hlmFieldLegend>
            {{ label(name) }} {{ schema.required?.includes(name) ? '*' : '' }}
          </legend>
          @for (item of array(name); track $index; let i = $index) {
            @if (kind(field.items ?? {}) === 'object') {
              <app-fields [schema]="field.items ?? {}" [model]="item" [prefix]="key + '-' + i" />
            } @else {
              <label hlmFieldLabel [for]="key + '-' + i">{{ label(name) }} {{ i + 1 }}</label
              ><input
                hlmInput
                [id]="key + '-' + i"
                [name]="key + '-' + i"
                [ngModelOptions]="{ standalone: true }"
                [(ngModel)]="model[name][i]"
                [required]="true"
              />
            }
            <button
              hlmBtn
              type="button"
              variant="outline"
              size="sm"
              (click)="model[name].splice(i, 1)"
              [attr.aria-label]="'Remove ' + label(name) + ' ' + (i + 1)"
            >
              Remove
            </button>
          }
          <button
            hlmBtn
            type="button"
            variant="outline"
            size="sm"
            [disabled]="array(name).length >= (field.maxItems ?? 100)"
            (click)="model[name].push(initial(field.items ?? {}))"
          >
            Add {{ label(name) }}
          </button>
        </fieldset>
      } @else {
        <div hlmField class="my-4">
          <label hlmFieldLabel [for]="key"
            >{{ label(name) }} {{ schema.required?.includes(name) ? '*' : '' }}</label
          >
          @if (field.enum) {
            <hlm-native-select
              [selectId]="key"
              [name]="key"
              [ngModelOptions]="{ standalone: true }"
              [ngModel]="model[name]"
              (ngModelChange)="model[name] = enumValue(field, $event)"
              [required]="(schema.required?.includes(name) ?? false) && !nullable(field)"
            >
              <option value="">Choose…</option>
              @for (value of field.enum; track $index) {
                <option [value]="value">{{ value }}</option>
              }
            </hlm-native-select>
          } @else if (kind(field) === 'boolean') {
            <hlm-checkbox
              [inputId]="key"
              [aria-label]="label(name)"
              [name]="key"
              [ngModelOptions]="{ standalone: true }"
              [(ngModel)]="model[name]"
            />
          } @else if (kind(field) === 'integer' || kind(field) === 'number') {
            <input
              hlmInput
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
              hlmTextarea
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
  enumValue(field: Schema, value: string) {
    return field.enum?.find((item) => String(item) === value) ?? value;
  }
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
