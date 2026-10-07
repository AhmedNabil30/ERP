import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

/** One row of a tree picker. `depth` is 0 for a top-level node; the picker indents it under the root row. */
export interface TreePickerNode {
  readonly id: string;
  readonly label: string;
  readonly code: string | null;
  readonly depth: number;
  /** Why this node cannot be chosen ("this باب", "one of its children"). Present means disabled. */
  readonly disabledReason?: string | null;
  /** The node the thing being moved sits under today. Marked with {@link KaffTreePicker.currentLabel}. */
  readonly current?: boolean;
}

let nextGroup = 0;

/**
 * The kit's tree picker (README "Kit additions", أبواب 3): a radio list with indentation on the start
 * edge, the current choice marked, and the node itself plus its descendants disabled with a reason so a
 * cycle cannot be built. "Move to…" and "choose a parent" are both this. No drag and drop anywhere.
 *
 * The first row is the root ("no parent", value `''`). Native radios, so the arrow keys, the group
 * label and the checked state come from the platform.
 */
@Component({
  selector: 'kaff-tree-picker',
  templateUrl: './kaff-tree-picker.html',
  styleUrl: './kaff-tree-picker.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffTreePicker {
  readonly nodes = input.required<readonly TreePickerNode[]>();
  readonly rootLabel = input.required<string>();
  /** The words on the row the thing sits under today. */
  readonly currentLabel = input.required<string>();
  /** True when the root row is the current parent. */
  readonly rootCurrent = input(false);
  readonly groupLabel = input.required<string>();
  readonly testIdPrefix = input('tree-picker-');
  /** The chosen node id; `''` is the root. */
  readonly value = model<string | null>(null);

  protected readonly name = `kaff-tree-picker-${++nextGroup}`;

  protected onChoose(id: string): void {
    this.value.set(id);
  }
}
