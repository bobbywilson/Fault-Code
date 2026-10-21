import { Component, Input, forwardRef } from '@angular/core';
import { label } from './schema';
@Component({
  selector: 'app-records',
  imports: [forwardRef(() => Records)],
  template: `
    @if (value === null || value === undefined) {
      <span class="muted">Unavailable</span>
    } @else if (isArray(value)) {
      @if (value.length === 0) {
        <p class="muted">No recorded items.</p>
      }
      @for (item of value; track $index) {
        <div class="record"><app-records [value]="item" /></div>
      }
    } @else if (isObject(value)) {
      <dl>
        @for (entry of entries(value); track entry[0]) {
          <div>
            <dt>{{ label(entry[0]) }}</dt>
            <dd><app-records [value]="entry[1]" /></dd>
          </div>
        }
      </dl>
    } @else {
      <span>{{ value }}</span>
    }
  `,
})
export class Records {
  @Input() value: any;
  label = label;
  isArray = Array.isArray;
  isObject(v: any) {
    return typeof v === 'object';
  }
  entries(v: any) {
    return Object.entries(v);
  }
}
