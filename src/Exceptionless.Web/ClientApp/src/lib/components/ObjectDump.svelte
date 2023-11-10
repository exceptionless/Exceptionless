<script lang="ts">
	export let value: unknown;

	let type = typeof value;
	let isBoolean = type === 'boolean' || value instanceof Boolean;
	let isObject = (type === 'object' || value instanceof Object) && value !== null;
	let isNull = value === null;
	let isEmptyValue = isEmpty(value);

	function isEmpty(value: unknown) {
		if (value === undefined) {
			return true;
		}

		if (value === null) {
			return false;
		}

		if (typeof value === 'object' || value instanceof Object) {
			return Object.keys(value || {}).length === 0;
		}

		if (Array.isArray(value)) {
			return value.length === 0;
		}

		return false;
	}
</script>

{#if isEmptyValue}
	(Empty)
{:else if Array.isArray(value)}
	<ul>
		{#each value as item}
			<li><svelte:self value={item} /></li>
		{/each}
	</ul>
{:else if isObject}
	<table class="table table-zebra table-xs border border-base-300">
		<tbody>
			{#each Object.entries(value || {}) as [key, val] (key)}
				<tr>
					<th class="border border-base-300 whitespace-nowrap">{key}</th>
					<td class="border border-base-300"><svelte:self value={val} /></td>
				</tr>
			{/each}
		</tbody>
	</table>
{:else if isBoolean}
	{value ? 'True' : 'False'}
{:else if isNull}
	(Null)
{:else}
	{value}
{/if}
