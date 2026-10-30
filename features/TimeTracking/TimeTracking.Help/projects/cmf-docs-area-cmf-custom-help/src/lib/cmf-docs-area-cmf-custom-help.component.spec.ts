import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CmfDocsAreaCmfCustomHelpComponent } from './cmf-docs-area-cmf-custom-help.component';

describe('CmfDocsAreaCmfCustomHelpComponent', () => {
  let component: CmfDocsAreaCmfCustomHelpComponent;
  let fixture: ComponentFixture<CmfDocsAreaCmfCustomHelpComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CmfDocsAreaCmfCustomHelpComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(CmfDocsAreaCmfCustomHelpComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
