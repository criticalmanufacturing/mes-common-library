import { bootstrapApplication } from '@angular/platform-browser';
import { loadApplicationConfig } from 'cmf-core/init';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

loadApplicationConfig('assets/config.json').then(() => {
  bootstrapApplication(AppComponent, appConfig)
    .catch((err) => console.error(err));
});
