import { Component, Input, OnChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgxGraphModule } from '@swimlane/ngx-graph';
import { Subject } from 'rxjs';

interface GraphNode {
  id: string;
  label: string;
  data: {
    type: 'object' | 'array';
    keyLabel: string;
    arrayLength: number;
    primitives: { key: string; displayValue: string; keyWidth: number; valueType: string }[];
  };
  dimension?: { width: number; height: number };
}

interface GraphEdge {
  id: string;
  source: string;
  target: string;
  label: string;
}

interface GraphZoomOptions {
  autoCenter?: boolean;
  force?: boolean;
}

const NODE_WIDTH = 220;
const NODE_HEADER_H = 28;
const NODE_ROW_H = 20;
const NODE_PADDING = 8;

@Component({
  selector: 'lib-json-graph-view',
  standalone: true,
  imports: [CommonModule, NgxGraphModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    :host {
      display: block;
      width: 100%;
      height: 100%;
    }

    .graph-wrapper {
      width: 100%;
      height: 100%;
      background: transparent;
    }

    .node-box {
      rx: 4;
      ry: 4;
      fill: var(--styleColor005);
      stroke: var(--styleColor012);
      stroke-width: 1;
    }

    .node-header {
      fill: var(--styleColor000);
      rx: 4;
      ry: 4;
    }

    .node-title {
      fill: var(--styleColor004);
      _font-family: open-sans-semibold, sans-serif;
      font-size: 12px;
      font-weight: 600;
    }

    .node-type {
      fill: var(--styleColor002);
      _font-family: open-sans-regular, sans-serif;
      font-size: 10px;
    }

    .row-key {
      fill: var(--styleColor004);
      _font-family: monospace;
      font-size: 11px;
    }

    .row-colon {
      fill: var(--styleColor002);
      _font-family: monospace;
      font-size: 11px;
    }

    .row-val-string {
      fill: var(--color-dark-green);
      _font-family: monospace;
      font-size: 11px;
    }

    .row-val-number {
      fill: var(--color-dark-blue);
      _font-family: monospace;
      font-size: 11px;
    }

    .row-val-boolean {
      fill: var(--color-purple);
      _font-family: monospace;
      font-size: 11px;
    }

    .row-val-null {
      fill: var(--styleColor002);
      _font-family: monospace;
      font-size: 11px;
    }

    .edge-line {
      stroke: var(--styleColor012);
      stroke-width: 1.25;
      fill: none;
    }

    .edge-label {
      fill: var(--styleColor002);
      _font-family: open-sans-regular, sans-serif;
      font-size: 10px;
    }
  `],
  template: `
    <ngx-graph
      class="graph-wrapper"
      layout="dagre"
      [nodes]="graphNodes"
      [links]="graphEdges"
      [autoZoom]="true"
      [autoCenter]="true"
      [centerNodesOnPositionChange]="true"
      [deferDisplayUntilPosition]="true"
      [enableZoom]="true"
      [panningEnabled]="true"
      [draggingEnabled]="true"
      [update$]="updateGraph$"
      [center$]="centerGraph$"
      [zoomToFit$]="zoomToFitGraph$">

      <ng-template #nodeTemplate let-node>
        <svg:g class="node">
          <svg:rect
            class="node-box"
            [attr.width]="node.dimension.width"
            [attr.height]="node.dimension.height">
          </svg:rect>
          <svg:rect
            class="node-header"
            [attr.width]="node.dimension.width"
            [attr.height]="NODE_HEADER_H"
            rx="4" ry="4">
          </svg:rect>
          <svg:text
            class="node-title"
            [attr.x]="8"
            [attr.y]="17">
            {{ node.data.keyLabel }}
          </svg:text>
          <svg:text
            class="node-type"
            [attr.x]="node.dimension.width - 8"
            [attr.y]="17"
            text-anchor="end">
            {{ node.data.type === 'array' ? '[' + node.data.arrayLength + ']' : '{}' }}
          </svg:text>
          <svg:g *ngFor="let row of node.data.primitives; let i = index"
                 [attr.transform]="'translate(8,' + (NODE_HEADER_H + NODE_PADDING + i * NODE_ROW_H + 14) + ')'">
            <svg:text class="row-key" x="0" y="0">"{{ row.key }}"</svg:text>
            <svg:text class="row-colon" [attr.x]="row.keyWidth + 2" y="0">:</svg:text>
            <svg:text
              [class]="'row-val-' + row.valueType"
              [attr.x]="row.keyWidth + 10"
              y="0">{{ row.displayValue }}</svg:text>
          </svg:g>
        </svg:g>
      </ng-template>

      <ng-template #linkTemplate let-link>
        <svg:g class="edge-line">
          <svg:path
            class="edge-line"
            [attr.d]="link.line">
          </svg:path>
        </svg:g>
      </ng-template>

    </ngx-graph>
  `
})
export class JsonGraphViewComponent implements OnChanges {
  @Input() data: any;

  public graphNodes: GraphNode[] = [];
  public graphEdges: GraphEdge[] = [];
  public readonly NODE_HEADER_H = NODE_HEADER_H;
  public readonly NODE_ROW_H = NODE_ROW_H;
  public readonly NODE_PADDING = NODE_PADDING;
  public updateGraph$ = new Subject<boolean>();
  public centerGraph$ = new Subject<boolean>();
  public zoomToFitGraph$ = new Subject<GraphZoomOptions>();

  public ngOnChanges(): void {
    const nodes: GraphNode[] = [];
    const edges: GraphEdge[] = [];
    this.buildGraph(null, this.data, this.getRootGraphId(), null, nodes, edges);
    this.graphNodes = nodes;
    this.graphEdges = edges;
    this.queueViewportReset();
  }

  private buildGraph(
    key: string | number | null,
    value: any,
    id: string,
    parentId: string | null,
    nodes: GraphNode[],
    edges: GraphEdge[]
  ): void {
    if (value === null || typeof value !== 'object') return;

    const isArray = Array.isArray(value);
    const entries = isArray
      ? (value as any[]).map((v, i) => ({ key: String(i), val: v }))
      : Object.keys(value).map(k => ({ key: k, val: value[k] }));

    const primitives = entries
      .filter(e => e.val === null || typeof e.val !== 'object')
      .map(e => ({
        key: e.key,
        displayValue: this.formatPrimitive(e.val),
        keyWidth: this.estimateTextWidth(e.key) + 16,
        valueType: this.valueType(e.val)
      }));

    const nodeHeight = NODE_HEADER_H + NODE_PADDING * 2 +
      (primitives.length > 0 ? primitives.length * NODE_ROW_H + 4 : 4);

    nodes.push({
      id,
      label: key !== null ? String(key) : 'root',
      data: {
        type: isArray ? 'array' : 'object',
        arrayLength: isArray ? value.length : 0,
        primitives,
        keyLabel: key !== null ? String(key) : 'root'
      },
      dimension: { width: NODE_WIDTH, height: nodeHeight }
    });

    if (parentId !== null) {
      edges.push({
        id: this.createEdgeId(parentId, id),
        source: parentId,
        target: id,
        label: key !== null ? String(key) : ''
      });
    }

    entries
      .filter(e => e.val !== null && typeof e.val === 'object')
      .forEach(e => {
        const childId = this.createChildGraphId(id, e.key);
        this.buildGraph(e.key, e.val, childId, id, nodes, edges);
      });
  }

  private getRootGraphId(): string {
    return 'node_root';
  }

  private createChildGraphId(parentId: string, key: string): string {
    return `${parentId}__${this.escapeGraphIdSegment(key)}`;
  }

  private createEdgeId(sourceId: string, targetId: string): string {
    return `edge_${sourceId}__${targetId}`;
  }

  private queueViewportReset(): void {
    const refreshDelays = [0, 40, 120];

    refreshDelays.forEach(delay => {
      setTimeout(() => {
        this.updateGraph$.next(true);
        this.zoomToFitGraph$.next({ autoCenter: true, force: true });
        this.centerGraph$.next(true);
      }, delay);
    });
  }

  private escapeGraphIdSegment(value: string): string {
    return String(value)
      .split('')
      .map(char => /[A-Za-z0-9_-]/.test(char)
        ? char
        : `_${char.charCodeAt(0).toString(16).padStart(4, '0')}`)
      .join('');
  }

  private formatPrimitive(v: any): string {
    if (v === null) return 'null';
    if (typeof v === 'string') {
      const s = `"${v}"`;
      return s.length > 24 ? s.slice(0, 23) + '…"' : s;
    }
    return String(v);
  }

  private valueType(v: any): string {
    if (v === null) return 'null';
    if (typeof v === 'string') return 'string';
    if (typeof v === 'number') return 'number';
    if (typeof v === 'boolean') return 'boolean';
    return 'null';
  }

  private estimateTextWidth(text: string): number {
    return text.length * 7;
  }
}
