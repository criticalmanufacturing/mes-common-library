import { Component, ElementRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  PropertyEditorModule,
  EntityPageService
} from 'cmf-core-business-controls';
import { PageBag, CodeEditorModule, CodeEditorLanguage } from "cmf-core-controls";
import { JsonTreeViewerComponent } from './json-tree-viewer.component';
import { JsonGraphViewComponent } from './json-graph-view.component';
import {
  FeedbackService,
  MessageBusService
} from 'cmf-core';


export interface PropertyEditorControlData {
  index: number,
  name: string,
}

@Component({
  selector: 'lib-customization-json-persistency',
  standalone: true,
  imports: [CommonModule, PropertyEditorModule, CodeEditorModule, JsonTreeViewerComponent, JsonGraphViewComponent],
  templateUrl: `./customization-json-persistency.component.html`,
  styleUrls: ['./customization-json-persistency.component.less']
})
/**
 * Component that allows selecting, visualizing, editing, and saving JSON persistency keys.
 */
export class CustomizationJsonPersistencyComponent {

  private _entityPage = inject(EntityPageService);
  private _elementRef = inject(ElementRef);
  private _pageBag = inject(PageBag);
  private _messageBus = inject(MessageBusService);
  private _feedback = inject(FeedbackService);

  private readonly _timeout = 1000;

  public activeListKeys: { name: string }[] = [];
  public keyValue: any;
  public editedValue: string = '';
  public selectedKey: { name: string } | null = null;
  public viewMode: 'tree' | 'editor' | 'split' = 'tree';
  public readonly CodeEditorLanguage = CodeEditorLanguage;

  private get automationControllerRequestTopic(): string {
    return `CMF.Cmf.Foundation.BusinessObjects.AutomationControllerInstance.${this._pageBag?.context?.id}.SENDREQUEST`;
  }

  /**
   * Indicates whether the current key value can be displayed in object-based views.
   */
  public get isKeyValueObject(): boolean {
    return this.keyValue !== null && typeof this.keyValue === 'object';
  }

  /**
   * Constructor
   */
  constructor() {
  }

  /**
   * Subscribes to entity load events and fetches persistency keys when context is available.
   */
  public ngOnInit(): void {

    this._entityPage.epEntityLoaded.subscribe(() => {
      this.resetView();
    });

    if (this._pageBag?.context?.id) {
      this.retrieveListOfPersistencyKeys();
    }
  }

  /**
   * Releases the entity loaded subscription to prevent leaks.
   */
  public ngOnDestroy(): void {
    this._entityPage.epEntityLoaded.unsubscribe();
  }

  /**
   * Updates the local edited value as the editor content changes.
   */
  public onEditorValueChange(value: string): void {
    this.editedValue = value;
  }

  /**
   * Loads the selected key payload and prepares its editable representation.
   */
  public async onControlValueChange(value: { name: string }) {

    // reset value if null is selected
    if (value === null) {
      this.selectedKey = null;
      this.keyValue = undefined;
      this.editedValue = '';
      return;
    }

    this.selectedKey = value;

    // send a message bus message to retrieve the persistency value for the selected key
    const result = (await this._feedback.progressIndicator(this._elementRef, this._messageBus
      .sendRequest(
        this.automationControllerRequestTopic,
        {
          "type": "GetPersistencyKeyValue",
          "data": value.name
        },
        this._timeout
      )))?.Data;

    this.keyValue = JSON.parse(result);
    // in case the reply is wrapped in a reply property
    this.keyValue = this.keyValue?.reply ?? this.keyValue;

    this._feedback.stopProgressIndicator(this._elementRef);
    this.editedValue = this.isKeyValueObject
      ? JSON.stringify(this.keyValue, null, 2)
      : String(this.keyValue ?? '');
  }

  /**
   * Persists the current edited value for the selected key.
   */
  public async saveKeyValue(): Promise<void> {
    if (!this.selectedKey) {
      return;
    }

    // send a message bus message to persist the edited value for the selected key
    await this._feedback.progressIndicator(this._elementRef, this._messageBus.sendRequest(
      this.automationControllerRequestTopic,
      {
        "type": "SetPersistencyKeyValue",
        "data": JSON.stringify({ key: this.selectedKey.name, value: this.editedValue })
      },
      this._timeout
    ));
  }

  /**
   * Retrieves the list of available persistency keys for the current context.
   */
  private async retrieveListOfPersistencyKeys() {
    this._feedback.startProgressIndicator(this._elementRef);
    this._messageBus
      .sendRequest(
        this.automationControllerRequestTopic,
        {
          "type": "GetPersistencyKeyList",
          "data": "Request"
        },
        this._timeout
      )
      .then((result) => {
        this._feedback.stopProgressIndicator(this._elementRef);
        this.activeListKeys = JSON.parse(result.Data).map((key: string) => { return { name: key } });
      })
      .catch((e) => {
        this._feedback.stopProgressIndicator(this._elementRef);
        throw new Error(
          $localize`:@@customization-json-persistency/customization-json-persistency#feedback.error:No Message Received with Persistency Key List: ${e}`
        );
      });
  }

  private resetView() {
    this.selectedKey = null;
    this.keyValue = undefined;
    this.editedValue = '';
    this.activeListKeys = [];
    this.retrieveListOfPersistencyKeys();
  }
}
