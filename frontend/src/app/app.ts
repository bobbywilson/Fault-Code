import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Auth } from './auth';
@Component({ selector: 'app-root', imports: [RouterOutlet], template: '<router-outlet />' })
export class App implements OnInit {
  auth = inject(Auth);
  ngOnInit() {
    void this.auth.initialize();
  }
}
