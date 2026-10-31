import { TestBed } from '@angular/core/testing';

import { CmfDocsAreaCmfCustomHelpService } from './cmf-docs-area-cmf-custom-help.service';

describe('CmfDocsAreaCmfCustomHelpService', () => {
  let service: CmfDocsAreaCmfCustomHelpService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CmfDocsAreaCmfCustomHelpService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
