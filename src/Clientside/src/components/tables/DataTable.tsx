import { useState } from 'react';
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  flexRender,
  type ColumnDef,
  type SortingState,
} from '@tanstack/react-table';
import { RiArrowUpSLine, RiArrowDownSLine } from '@remixicon/react';

interface DataTableProps<T> {
  columns: ColumnDef<T, unknown>[];
  data: T[];
  totalCount?: number;
  page?: number;
  pageSize?: number;
  onPageChange?: (page: number) => void;
}

export function DataTable<T>({
  columns,
  data,
  totalCount,
  page = 1,
  pageSize = 20,
  onPageChange,
}: DataTableProps<T>) {
  const [sorting, setSorting] = useState<SortingState>([]);

  const table = useReactTable({
    data,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  });

  const totalPages = totalCount != null ? Math.ceil(totalCount / pageSize) : 1;

  return (
    <div className="overflow-x-auto">
      <table className="w-full">
        <thead>
          {table.getHeaderGroups().map((headerGroup) => (
            <tr key={headerGroup.id} className="border-b border-slate-100">
              {headerGroup.headers.map((header) => (
                <th
                  key={header.id}
                  className={`px-4 py-3 text-left text-[11px] font-medium uppercase tracking-[0.05em] text-slate-400 ${
                    header.column.getCanSort()
                      ? 'cursor-pointer select-none hover:text-slate-600'
                      : ''
                  }`}
                  onClick={header.column.getToggleSortingHandler()}
                >
                  <div className="flex items-center gap-1">
                    {flexRender(header.column.columnDef.header, header.getContext())}
                    {header.column.getIsSorted() === 'asc' && (
                      <RiArrowUpSLine className="h-3.5 w-3.5" />
                    )}
                    {header.column.getIsSorted() === 'desc' && (
                      <RiArrowDownSLine className="h-3.5 w-3.5" />
                    )}
                  </div>
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody className="divide-y divide-slate-100/60">
          {table.getRowModel().rows.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="px-4 py-8 text-center text-sm text-slate-400">
                No data available
              </td>
            </tr>
          ) : (
            table.getRowModel().rows.map((row) => (
              <tr key={row.id} className="hover:bg-slate-50/60 transition-colors duration-100">
                {row.getVisibleCells().map((cell) => (
                  <td key={cell.id} className="px-4 py-3.5 text-sm text-slate-600">
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>

      {onPageChange && totalCount != null && totalPages > 1 && (
        <div className="flex items-center justify-between px-4 py-3.5 border-t border-slate-100">
          <p className="text-xs text-slate-400">
            Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of{' '}
            {totalCount}
          </p>
          <div className="flex gap-2">
            <button
              onClick={() => onPageChange(page - 1)}
              disabled={page <= 1}
              className="px-3.5 py-1.5 text-xs font-medium rounded-lg border border-slate-200 text-slate-500 disabled:opacity-40 hover:bg-slate-50 hover:border-slate-300 transition-all"
            >
              Previous
            </button>
            <button
              onClick={() => onPageChange(page + 1)}
              disabled={page >= totalPages}
              className="px-3.5 py-1.5 text-xs font-medium rounded-lg border border-slate-200 text-slate-500 disabled:opacity-40 hover:bg-slate-50 hover:border-slate-300 transition-all"
            >
              Next
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
