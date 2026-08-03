/// <reference types="@angular/localize" />

import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { loadApplicationConfig } from 'cmf-core/init';

loadApplicationConfig('assets/config.json').then(() => {
  import(/* webpackMode: "eager" */ './app/app.config').then(({ appConfig }) => {
    bootstrapApplication(AppComponent, appConfig)
      .catch((err) => console.error(err));
  });
});
