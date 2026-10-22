import { Component, Input, forwardRef } from '@angular/core';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { label } from './schema';
@Component({
  selector: 'app-records',
  imports: [HlmTableImports, HlmCardImports, forwardRef(() => Records)],
  template: `
    @if (value === null || value === undefined) {
      <span class="text-sm text-muted-foreground">Unavailable</span>
    } @else if (isArray(value)) {
      @if (value.length === 0) {
        <p class="text-sm text-muted-foreground">No recorded items.</p>
      }
      @for (item of value; track $index) {
        <div hlmCard class="my-3 p-4"><app-records [value]="item" /></div>
      }
    } @else if (isObject(value)) {
      <div hlmTableContainer>
        <table hlmTable>
          <tbody hlmTBody>
            @for (entry of entries(value); track entry[0]) {
              <tr hlmTr>
                <th hlmTh scope="row" class="w-44 whitespace-normal align-top">
                  {{ label(entry[0]) }}
                </th>
                <td hlmTd class="whitespace-normal break-words">
                  <app-records [value]="entry[1]" />
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
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
