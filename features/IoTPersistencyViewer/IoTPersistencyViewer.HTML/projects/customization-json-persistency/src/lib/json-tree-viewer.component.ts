import { Component, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';

interface TreeNode {
  key: string | number | null;
  value: any;
  type: 'object' | 'array' | 'string' | 'number' | 'boolean' | 'null';
  children?: TreeNode[];
  path: string;
}

@Component({
  selector: 'lib-json-tree-viewer',
  standalone: true,
  imports: [CommonModule],
  styles: [`
    :host {
      display: block;
      width: 100%;
    }

    .property-container {
      flex-direction: column;
      width: 100%;
    }

    .property-value {
      _font-family: open-sans-regular, sans-serif;
      width: 100%;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      min-height: 0;
    }

    /* Tree styles */
    .toolbar { display: flex; gap: 8px; margin-bottom: 8px; }
    .toolbar button {
      font-size: 12px;
      padding: 2px 10px;
      cursor: pointer;
      border: 1px solid var(--styleColor012);
      border-radius: 3px;
      background: var(--styleColor005);
      color: var(--styleColor004);
    }
    .toolbar button:hover {
      border-color: var(--styleColor003);
      background: var(--styleColor000);
    }
    .json-tree { _font-family: monospace; font-size: 13px; line-height: 1.6; width: 100%; }
    .node { padding-left: 16px; }
    .node-line { display: flex; align-items: center; gap: 4px; white-space: nowrap; }
    .toggle { cursor: pointer; user-select: none; color: var(--styleColor002); width: 12px; display: inline-block; flex-shrink: 0; }
    .key { color: var(--styleColor001); }
    .colon { color: var(--styleColor002); margin-right: 4px; }
    .brace { color: var(--styleColor002); }
    .summary { color: var(--styleColor002); font-style: italic; }
    .val-string { color: var(--color-dark-green); }
    .val-number { color: var(--color-dark-blue); }
    .val-boolean { color: var(--color-purple); }
    .val-null { color: var(--styleColor002); }
  `],
  template: `
    <div class="property-container">
      <div class="property-value">
        <div class="toolbar">
          <button (click)="expandAll()">Expand All</button>
          <button (click)="collapseAll()">Collapse All</button>
        </div>
        <div class="json-tree">
          <ng-container *ngTemplateOutlet="nodeTemplate; context: { node: root, depth: 0 }"></ng-container>
        </div>
      </div>
    </div>

    <ng-template #nodeTemplate let-node="node" let-depth="depth">
      <div class="node" [style.padding-left.px]="depth === 0 ? 0 : 16">
        <div class="node-line">
          <!-- Toggle arrow for objects/arrays -->
          <span class="toggle" *ngIf="node.type === 'object' || node.type === 'array'"
                (click)="toggle(node.path)">
            {{ isCollapsed(node.path) ? '▶' : '▼' }}
          </span>
          <span class="toggle" *ngIf="node.type !== 'object' && node.type !== 'array'"></span>

          <!-- Key -->
          <span class="key" *ngIf="node.key !== null">"{{ node.key }}"</span>
          <span class="colon" *ngIf="node.key !== null">:</span>

          <!-- Collapsed summary for object/array -->
          <ng-container *ngIf="node.type === 'object' || node.type === 'array'">
            <span class="brace">{{ node.type === 'object' ? '{' : '[' }}</span>
            <ng-container *ngIf="isCollapsed(node.path)">
              <span class="summary">&nbsp;{{ node.type === 'object' ? 'Object' : 'Array' }}{{'{'}}{{ node.children?.length }}{{'}'}}&nbsp;</span>
              <span class="brace">{{ node.type === 'object' ? '}' : ']' }}</span>
            </ng-container>
          </ng-container>

          <!-- Primitive values -->
          <span *ngIf="node.type === 'string'" class="val-string">"{{ node.value }}"</span>
          <span *ngIf="node.type === 'number'" class="val-number">{{ node.value }}</span>
          <span *ngIf="node.type === 'boolean'" class="val-boolean">{{ node.value }}</span>
          <span *ngIf="node.type === 'null'" class="val-null">null</span>
        </div>

        <!-- Expanded children -->
        <ng-container *ngIf="(node.type === 'object' || node.type === 'array') && !isCollapsed(node.path)">
          <ng-container *ngFor="let child of node.children">
            <ng-container *ngTemplateOutlet="nodeTemplate; context: { node: child, depth: depth + 1 }"></ng-container>
          </ng-container>
          <div class="node-line" [style.padding-left.px]="0">
            <span class="toggle"></span>
            <span class="brace">{{ node.type === 'object' ? '}' : ']' }}</span>
          </div>
        </ng-container>
      </div>
    </ng-template>
  `
})
export class JsonTreeViewerComponent implements OnChanges {
  @Input() data: any;

  public root!: TreeNode;
  private collapsed = new Set<string>();

  public ngOnChanges(): void {
    this.root = this.buildNode(null, this.data, 'root');
  }

  public isCollapsed(path: string): boolean {
    return this.collapsed.has(path);
  }

  public toggle(path: string): void {
    if (this.collapsed.has(path)) {
      this.collapsed.delete(path);
    } else {
      this.collapsed.add(path);
    }
  }

  public expandAll(): void {
    this.collapsed.clear();
  }

  public collapseAll(): void {
    this.collectPaths(this.root);
  }

  private collectPaths(node: TreeNode): void {
    if (node.type === 'object' || node.type === 'array') {
      this.collapsed.add(node.path);
      node.children?.forEach(c => this.collectPaths(c));
    }
  }

  private buildNode(key: string | number | null, value: any, path: string): TreeNode {
    if (value === null) {
      return { key, value, type: 'null', path };
    }
    if (Array.isArray(value)) {
      const children = value.map((v, i) => this.buildNode(i, v, `${path}[${i}]`));
      return { key, value, type: 'array', children, path };
    }
    if (typeof value === 'object') {
      const children = Object.keys(value).map(k => this.buildNode(k, value[k], `${path}.${k}`));
      return { key, value, type: 'object', children, path };
    }
    if (typeof value === 'string') return { key, value, type: 'string', path };
    if (typeof value === 'number') return { key, value, type: 'number', path };
    if (typeof value === 'boolean') return { key, value, type: 'boolean', path };
    return { key, value, type: 'string', path };
  }
}
