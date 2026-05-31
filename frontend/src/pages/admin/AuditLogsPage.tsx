import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ShieldAlert, Search, Filter, ChevronLeft, ChevronRight, Calendar, X, RefreshCw, FileSearch, Download } from 'lucide-react';
import { auditLogsApi, type AuditLog, type AuditLogFilters } from '../../api/auditLogsApi';
import { adminUsersApi } from '../../api/adminUsersApi';
import { downloadFile } from '../../utils/download';
import { useAuthStore } from '../../store/authStore';
import toast from 'react-hot-toast';

export default function AuditLogsPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const isAdmin = user?.role?.toLowerCase() === 'admin';

  useEffect(() => {
    if (!isAdmin) {
      toast.error('Admin access required');
      navigate('/dashboard');
    }
  }, [isAdmin, navigate]);

  const [filters, setFilters] = useState<AuditLogFilters>({ pageNumber: 1, pageSize: 25 });
  const [searchInput, setSearchInput] = useState('');
  const [showFilters, setShowFilters] = useState(false);
  const [expandedRow, setExpandedRow] = useState<string | null>(null);

  // Debounce search input
  useEffect(() => {
    const t = setTimeout(() => {
      setFilters(f => ({ ...f, search: searchInput || undefined, pageNumber: 1 }));
    }, 400);
    return () => clearTimeout(t);
  }, [searchInput]);

  const { data: logs, isLoading, refetch } = useQuery({
    queryKey: ['audit-logs', filters],
    queryFn: () => auditLogsApi.list(filters),
    enabled: isAdmin,
  });

  const { data: filterOpts } = useQuery({
    queryKey: ['audit-log-filters'],
    queryFn: () => auditLogsApi.filters(),
    enabled: isAdmin,
  });

  const { data: usersResp } = useQuery({
    queryKey: ['admin-users-list'],
    queryFn: () => adminUsersApi.list({}),
    enabled: isAdmin,
  });

  if (!isAdmin) return null;

  const data = (logs as any)?.data || [];
  const total = (logs as any)?.totalCount || 0;
  const totalPages = (logs as any)?.totalPages || 1;
  const pageNumber = (logs as any)?.pageNumber || 1;

  const actionColor = (action: string): string => {
    const a = action.toLowerCase();
    if (a.includes('delete') || a.includes('remove')) return 'bg-red-100 text-red-700';
    if (a.includes('create') || a.includes('add')) return 'bg-green-100 text-green-700';
    if (a.includes('update') || a.includes('edit') || a.includes('change')) return 'bg-blue-100 text-blue-700';
    if (a.includes('login') || a.includes('logout')) return 'bg-purple-100 text-purple-700';
    if (a.includes('send') || a.includes('campaign')) return 'bg-amber-100 text-amber-700';
    return 'bg-gray-100 text-gray-700';
  };

  const clearFilters = () => {
    setFilters({ pageNumber: 1, pageSize: 25 });
    setSearchInput('');
  };

  const hasActiveFilters = !!(filters.userId || filters.action || filters.entity || filters.fromDate || filters.toDate || filters.search);

  const prettyDetails = (details?: string | null): string => {
    if (!details) return '—';
    try {
      const parsed = JSON.parse(details);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return details;
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-red-100 rounded-xl">
            <ShieldAlert className="w-6 h-6 text-red-600" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Audit Logs</h1>
            <p className="text-sm text-gray-500">All sensitive actions across the platform</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={async () => {
              try {
                await downloadFile(
                  auditLogsApi.exportCsvUrl(filters),
                  `audit_logs_${new Date().toISOString().slice(0, 10)}.csv`,
                );
              } catch (e: any) {
                toast.error(e?.message || 'Export failed');
              }
            }}
            className="flex items-center gap-2 px-4 py-2 border border-gray-200 rounded-xl text-sm font-medium hover:bg-gray-50"
            title="Download current filtered view as CSV"
          >
            <Download className="w-4 h-4" />
            Export CSV
          </button>
          <button
            onClick={() => refetch()}
            className="flex items-center gap-2 px-4 py-2 border border-gray-200 rounded-xl text-sm font-medium hover:bg-gray-50"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
        </div>
      </div>

      {/* Search + Filter Toggle */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
        <div className="flex items-center gap-3">
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Search by action, entity, user, or details..."
              className="w-full pl-10 pr-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none"
            />
          </div>
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={`flex items-center gap-2 px-4 py-2.5 border rounded-xl text-sm font-medium transition-colors ${
              showFilters || hasActiveFilters
                ? 'bg-primary-50 border-primary-300 text-primary-700'
                : 'border-gray-200 hover:bg-gray-50'
            }`}
          >
            <Filter className="w-4 h-4" />
            Filters {hasActiveFilters && <span className="px-1.5 py-0.5 bg-primary-600 text-white text-[10px] rounded-full">{Object.values(filters).filter(v => v && typeof v !== 'number').length}</span>}
          </button>
          {hasActiveFilters && (
            <button
              onClick={clearFilters}
              className="flex items-center gap-1 px-3 py-2.5 text-red-600 hover:bg-red-50 rounded-xl text-sm font-medium"
            >
              <X className="w-4 h-4" />
              Clear
            </button>
          )}
        </div>

        {showFilters && (
          <div className="mt-4 pt-4 border-t border-gray-100 grid grid-cols-1 md:grid-cols-4 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">User</label>
              <select
                value={filters.userId || ''}
                onChange={(e) => setFilters({ ...filters, userId: e.target.value || undefined, pageNumber: 1 })}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm"
              >
                <option value="">All users</option>
                {((usersResp as any)?.data || []).map((u: any) => (
                  <option key={u.id} value={u.id}>{u.fullName} ({u.email})</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Action</label>
              <select
                value={filters.action || ''}
                onChange={(e) => setFilters({ ...filters, action: e.target.value || undefined, pageNumber: 1 })}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm"
              >
                <option value="">All actions</option>
                {((filterOpts as any)?.data?.actions || []).map((a: string) => (
                  <option key={a} value={a}>{a}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Entity</label>
              <select
                value={filters.entity || ''}
                onChange={(e) => setFilters({ ...filters, entity: e.target.value || undefined, pageNumber: 1 })}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm"
              >
                <option value="">All entities</option>
                {((filterOpts as any)?.data?.entities || []).map((en: string) => (
                  <option key={en} value={en}>{en}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Page Size</label>
              <select
                value={filters.pageSize}
                onChange={(e) => setFilters({ ...filters, pageSize: parseInt(e.target.value), pageNumber: 1 })}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm"
              >
                <option value={10}>10</option>
                <option value={25}>25</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>
            </div>
            <div className="md:col-span-2">
              <label className="block text-xs font-medium text-gray-600 mb-1">From Date</label>
              <input
                type="datetime-local"
                value={filters.fromDate ? filters.fromDate.slice(0, 16) : ''}
                onChange={(e) => setFilters({ ...filters, fromDate: e.target.value ? new Date(e.target.value).toISOString() : undefined, pageNumber: 1 })}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm"
              />
            </div>
            <div className="md:col-span-2">
              <label className="block text-xs font-medium text-gray-600 mb-1">To Date</label>
              <input
                type="datetime-local"
                value={filters.toDate ? filters.toDate.slice(0, 16) : ''}
                onChange={(e) => setFilters({ ...filters, toDate: e.target.value ? new Date(e.target.value).toISOString() : undefined, pageNumber: 1 })}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm"
              />
            </div>
          </div>
        )}
      </div>

      {/* Results */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
          <p className="text-sm text-gray-600">
            <span className="font-semibold text-gray-900">{total.toLocaleString()}</span> records
          </p>
          <p className="text-xs text-gray-400">Page {pageNumber} of {totalPages}</p>
        </div>

        {isLoading ? (
          <div className="py-16 flex items-center justify-center">
            <div className="w-8 h-8 border-4 border-primary-500 border-t-transparent rounded-full animate-spin" />
          </div>
        ) : data.length === 0 ? (
          <div className="py-16 text-center">
            <FileSearch className="w-12 h-12 text-gray-200 mx-auto mb-3" />
            <p className="text-gray-500 text-sm">No audit logs match your filters</p>
            {hasActiveFilters && (
              <button onClick={clearFilters} className="mt-2 text-primary-600 text-sm hover:underline">Clear filters</button>
            )}
          </div>
        ) : (
          <div className="divide-y divide-gray-50">
            {data.map((log: AuditLog) => (
              <div key={log.id} className="hover:bg-gray-50/50 transition-colors">
                <div
                  onClick={() => setExpandedRow(expandedRow === log.id ? null : log.id)}
                  className="px-4 py-3 cursor-pointer flex items-start gap-3"
                >
                  <span className={`px-2.5 py-1 rounded-lg text-xs font-semibold ${actionColor(log.action)} whitespace-nowrap`}>
                    {log.action}
                  </span>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center justify-between flex-wrap gap-2">
                      <div className="text-sm text-gray-900">
                        {log.userFullName || <span className="text-gray-400 italic">System / anonymous</span>}
                        {log.userEmail && <span className="text-xs text-gray-400 ml-2">({log.userEmail})</span>}
                      </div>
                      <div className="flex items-center gap-1 text-xs text-gray-400 whitespace-nowrap">
                        <Calendar className="w-3 h-3" />
                        {new Date(log.createdAt).toLocaleString()}
                      </div>
                    </div>
                    <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-gray-500">
                      {log.entity && (
                        <span>
                          on <span className="font-medium text-gray-700">{log.entity}</span>
                          {log.entityId && <span className="ml-1 font-mono text-[10px] text-gray-400">{log.entityId.slice(0, 8)}…</span>}
                        </span>
                      )}
                      {log.ipAddress && <span>· IP {log.ipAddress}</span>}
                    </div>
                  </div>
                </div>
                {expandedRow === log.id && log.details && (
                  <div className="px-4 pb-3 -mt-1">
                    <div className="bg-gray-900 text-gray-100 rounded-lg p-3 text-xs font-mono overflow-x-auto">
                      <pre className="whitespace-pre-wrap break-all">{prettyDetails(log.details)}</pre>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}

        {/* Pagination */}
        {data.length > 0 && totalPages > 1 && (
          <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
            <p className="text-xs text-gray-500">
              Showing {((pageNumber - 1) * filters.pageSize!) + 1}–{Math.min(pageNumber * filters.pageSize!, total)} of {total}
            </p>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setFilters({ ...filters, pageNumber: pageNumber - 1 })}
                disabled={pageNumber <= 1}
                className="flex items-center gap-1 px-3 py-1.5 border border-gray-200 rounded-lg text-sm hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <ChevronLeft className="w-4 h-4" />
                Previous
              </button>
              <span className="text-sm text-gray-600">
                {pageNumber} / {totalPages}
              </span>
              <button
                onClick={() => setFilters({ ...filters, pageNumber: pageNumber + 1 })}
                disabled={pageNumber >= totalPages}
                className="flex items-center gap-1 px-3 py-1.5 border border-gray-200 rounded-lg text-sm hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Next
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
