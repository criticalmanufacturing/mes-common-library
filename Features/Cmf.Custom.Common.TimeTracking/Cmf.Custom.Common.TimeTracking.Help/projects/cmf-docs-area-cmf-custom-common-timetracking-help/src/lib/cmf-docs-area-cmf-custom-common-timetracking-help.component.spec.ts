import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CmfDocsAreaCmfCustomCommonTimetrackingHelpComponent } from './cmf-docs-area-cmf-custom-common-timetracking-help.component';

describe('CmfDocsAreaCmfCustomCommonTimetrackingHelpComponent', () => {
  let component: CmfDocsAreaCmfCustomCommonTimetrackingHelpComponent;
  let fixture: ComponentFixture<CmfDocsAreaCmfCustomCommonTimetrackingHelpComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CmfDocsAreaCmfCustomCommonTimetrackingHelpComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(CmfDocsAreaCmfCustomCommonTimetrackingHelpComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
