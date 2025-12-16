import { TestBed } from '@angular/core/testing';

import { CmfDocsAreaCmfCustomCommonTimetrackingHelpService } from './cmf-docs-area-cmf-custom-common-timetracking-help.service';

describe('CmfDocsAreaCmfCustomCommonTimetrackingHelpService', () => {
  let service: CmfDocsAreaCmfCustomCommonTimetrackingHelpService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CmfDocsAreaCmfCustomCommonTimetrackingHelpService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
