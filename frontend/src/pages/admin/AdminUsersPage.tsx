import { useEffect, useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Users, Plus, Search, Shield, User as UserIcon, Edit2, Trash2, KeyRound,
  X, Eye, EyeOff, CheckCircle2, AlertTriangle, Loader2, RefreshCw,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { adminUsersApi, type AdminUser, type CreateUserPayload, type UpdateUserPayload } from '../../api/adminUsersApi';
import { smtpGroupsApi, type SmtpGroup } from '../../api/smtpGroupsApi';
import { useAuthStore } from '../../store/authStore';

const blank: CreateUserPayload = { fullName: '', email: '', password: '', role: 'user', smtpGroupId: null };

export default function AdminUsersPage() {
  const me = useAuthStore((s) => s.user);
  const isAdmin = me?.role?.toLowerCase() === 'admin';
  if (me && !isAdmin) return <Navigate to="/dashboard" replace />;

  const [users, setUsers] = useState<AdminUser[]>([]);
  const [groups, setGroups] = useState<SmtpGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState<string>('');
  const [groupFilter, setGroupFilter] = useState<string>('');
  const [activeOnly, setActiveOnly] = useState(true);

  // Modals
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState<CreateUserPayload>(blank);
  const [showCreatePw, setShowCreatePw] = useState(false);
  const [editing, setEditing] = useState<AdminUser | null>(null);
  const [editForm, setEditForm] = useState<UpdateUserPayload>({ fullName: '', email: '', smtpGroupId: null, isActive: true });
  const [resettingUser, setResettingUser] = useState<AdminUser | null>(null);
  const [resetPwValue, setResetPwValue] = useState('');
  const [saving, setSaving] = useState(false);

  const load = async () => {
    try {
      const [usersRes, groupsRes]: any[] = await Promise.all([
        adminUsersApi.list({ search: search || undefined, role: roleFilter || undefined, smtpGroupId: groupFilter || undefined, isActive: activeOnly ? true : undefined }),
        smtpGroupsApi.list(),
      ]);
      setUsers(usersRes.data || []);
      setGroups(groupsRes.data || []);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to load users');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, []);
  useEffect(() => { const t = setTimeout(load, 200); return () => clearTimeout(t); /* eslint-disable-next-line */ }, [search, roleFilter, groupFilter, activeOnly]);

  const stats = useMemo(() => {
    const total = users.length;
    const admins = users.filter(u => u.role === 'admin').length;
    const active = users.filter(u => u.isActive).length;
    const unassigned = users.filter(u => !u.smtpGroupId && u.role !== 'admin').length;
    return { total, admins, regular: total - admins, active, inactive: total - active, unassigned };
  }, [users]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await adminUsersApi.create(createForm);
      toast.success(`User created: ${createForm.email}`);
      setCreating(false);
      setCreateForm(blank);
      load();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to create user');
    } finally { setSaving(false); }
  };

  const handleSaveEdit = async () => {
    if (!editing) return;
    setSaving(true);
    try {
      await adminUsersApi.update(editing.id, editForm);
      toast.success('User updated');
      setEditing(null);
      load();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Update failed');
    } finally { setSaving(false); }
  };

  const handleRoleChange = async (u: AdminUser, newRole: 'admin' | 'user') => {
    if (newRole === u.role) return;
    if (!confirm(`Change ${u.fullName}'s role to ${newRole}?`)) return;
    try {
      await adminUsersApi.changeRole(u.id, newRole);
      toast.success(`${u.fullName} is now ${newRole}`);
      load();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Role change failed');
    }
  };

  const handleResetPassword = async () => {
    if (!resettingUser) return;
    setSaving(true);
    try {
      await adminUsersApi.resetPassword(resettingUser.id, resetPwValue);
      toast.success('Password reset. Share the new password securely.');
      setResettingUser(null);
      setResetPwValue('');
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Reset failed');
    } finally { setSaving(false); }
  };

  const handleDeactivate = async (u: AdminUser) => {
    if (!confirm(`Deactivate ${u.fullName}? Their data is preserved — you can reactivate by editing.`)) return;
    try {
      await adminUsersApi.delete(u.id);
      toast.success('User deactivated');
      load();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Deactivation failed');
    }
  };

  const inputCls = "w-full px-3 py-2 border border-gray-200 rounded-lg focus:ring-2 focus:ring-indigo-500 outline-none text-sm bg-gray-50 focus:bg-white";

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-gradient-to-br from-red-500 to-orange-600 rounded-xl">
            <Users className="w-6 h-6 text-white" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
              User Management
              <span className="px-2 py-0.5 bg-red-100 text-red-700 text-[10px] font-semibold rounded-full uppercase tracking-wide">Admin</span>
            </h1>
            <p className="text-gray-500 text-sm mt-0.5">Create, edit, change roles, reset passwords, deactivate users.</p>
          </div>
        </div>
        <button
          onClick={() => { setCreateForm(blank); setCreating(true); }}
          className="flex items-center gap-2 px-4 py-2.5 bg-red-600 text-white rounded-xl font-medium hover:bg-red-700 shadow-sm"
        >
          <Plus className="w-4 h-4" /> New User
        </button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
        {[
          { label: 'Total', value: stats.total, color: 'gray', Icon: Users },
          { label: 'Admins', value: stats.admins, color: 'red', Icon: Shield },
          { label: 'Users', value: stats.regular, color: 'indigo', Icon: UserIcon },
          { label: 'Inactive', value: stats.inactive, color: 'amber', Icon: AlertTriangle },
          { label: 'No SMTP Group', value: stats.unassigned, color: 'orange', Icon: AlertTriangle },
        ].map(s => (
          <div key={s.label} className="bg-white rounded-xl border border-gray-100 p-3">
            <div className="flex items-center justify-between">
              <span className={`text-${s.color}-600`}><s.Icon className="w-4 h-4" /></span>
              <span className="text-[10px] uppercase tracking-wide text-gray-500 font-medium">{s.label}</span>
            </div>
            <p className={`text-2xl font-bold tabular-nums text-${s.color === 'gray' ? 'gray-900' : `${s.color}-700`}`}>{s.value}</p>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="bg-white rounded-xl border border-gray-100 p-3 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input type="text" value={search} onChange={e => setSearch(e.target.value)} placeholder="Search by name or email..." className={inputCls + ' pl-9'} />
        </div>
        <select value={roleFilter} onChange={e => setRoleFilter(e.target.value)} className={inputCls + ' w-auto'}>
          <option value="">All roles</option>
          <option value="admin">Admins</option>
          <option value="user">Users</option>
        </select>
        <select value={groupFilter} onChange={e => setGroupFilter(e.target.value)} className={inputCls + ' w-auto'}>
          <option value="">All SMTP groups</option>
          {groups.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
        </select>
        <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
          <input type="checkbox" checked={activeOnly} onChange={e => setActiveOnly(e.target.checked)} className="w-4 h-4 text-indigo-600 rounded" />
          Active only
        </label>
        <button onClick={load} className="p-2 text-gray-400 hover:text-gray-700 hover:bg-gray-50 rounded-lg" title="Refresh">
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      {/* Users table */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        {loading ? (
          <div className="p-12 text-center"><Loader2 className="w-8 h-8 animate-spin text-red-500 mx-auto" /></div>
        ) : users.length === 0 ? (
          <div className="p-12 text-center text-gray-400">
            <Users className="w-10 h-10 mx-auto mb-2 opacity-50" />
            <p className="font-medium">No users match your filters</p>
          </div>
        ) : (
          <table className="w-full">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-100">
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Name</th>
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Email</th>
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Role</th>
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">SMTP Group</th>
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Status</th>
                <th className="text-right px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {users.map(u => (
                <tr key={u.id} className={`hover:bg-gray-50 transition-colors ${!u.isActive ? 'opacity-60' : ''}`}>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2.5">
                      <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-semibold text-white ${u.role === 'admin' ? 'bg-gradient-to-br from-red-500 to-orange-500' : 'bg-gradient-to-br from-indigo-500 to-purple-500'}`}>
                        {(u.fullName || '?').charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <p className="text-sm font-medium text-gray-900 flex items-center gap-1.5">
                          {u.fullName}
                          {u.hasPersonalSignature && <span title="Has personal signature" className="text-[10px] text-pink-500">✍️</span>}
                        </p>
                        <p className="text-[11px] text-gray-400">Joined {new Date(u.createdAt).toLocaleDateString()}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-700">{u.email}</td>
                  <td className="px-4 py-3">
                    <select
                      value={u.role}
                      onChange={e => handleRoleChange(u, e.target.value as 'admin' | 'user')}
                      disabled={u.id === me?.id}
                      className={`px-2 py-1 text-xs font-medium rounded-lg border ${u.role === 'admin' ? 'bg-red-50 text-red-700 border-red-200' : 'bg-indigo-50 text-indigo-700 border-indigo-200'} ${u.id === me?.id ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
                      title={u.id === me?.id ? "You can't change your own role" : 'Change role'}
                    >
                      <option value="user">User</option>
                      <option value="admin">Admin</option>
                    </select>
                  </td>
                  <td className="px-4 py-3">
                    {u.smtpGroupName ? (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 bg-blue-50 text-blue-700 text-[11px] font-medium rounded">
                        {u.smtpGroupName}
                      </span>
                    ) : (
                      <span className="text-xs text-gray-400">— Not assigned</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {u.isActive ? (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 bg-emerald-50 text-emerald-700 text-[11px] font-medium rounded-full">
                        <CheckCircle2 className="w-3 h-3" /> Active
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 bg-gray-100 text-gray-500 text-[11px] font-medium rounded-full">Inactive</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <div className="inline-flex items-center gap-1">
                      <button
                        onClick={() => { setEditing(u); setEditForm({ fullName: u.fullName, email: u.email, smtpGroupId: u.smtpGroupId ?? null, isActive: u.isActive }); }}
                        className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg"
                        title="Edit"
                      >
                        <Edit2 className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => { setResettingUser(u); setResetPwValue(''); }}
                        className="p-1.5 text-gray-400 hover:text-amber-600 hover:bg-amber-50 rounded-lg"
                        title="Reset password"
                      >
                        <KeyRound className="w-4 h-4" />
                      </button>
                      {u.isActive && u.id !== me?.id && (
                        <button
                          onClick={() => handleDeactivate(u)}
                          className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg"
                          title="Deactivate"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* ─── CREATE MODAL ─── */}
      {creating && (
        <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <form onSubmit={handleCreate} className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold text-gray-900">Create User</h3>
              <button type="button" onClick={() => setCreating(false)} className="p-1 hover:bg-gray-100 rounded"><X className="w-5 h-5" /></button>
            </div>
            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Full Name *</label>
                <input type="text" required value={createForm.fullName} onChange={e => setCreateForm({...createForm, fullName: e.target.value})} className={inputCls} />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Email *</label>
                <input type="email" required value={createForm.email} onChange={e => setCreateForm({...createForm, email: e.target.value})} className={inputCls} />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Password *</label>
                <div className="relative">
                  <input type={showCreatePw ? 'text' : 'password'} required value={createForm.password} onChange={e => setCreateForm({...createForm, password: e.target.value})} className={inputCls + ' pr-9'} />
                  <button type="button" onClick={() => setShowCreatePw(!showCreatePw)} className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400">
                    {showCreatePw ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Role</label>
                  <select value={createForm.role} onChange={e => setCreateForm({...createForm, role: e.target.value as 'admin' | 'user'})} className={inputCls}>
                    <option value="user">User</option>
                    <option value="admin">Admin</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">SMTP Group</label>
                  <select value={createForm.smtpGroupId ?? ''} onChange={e => setCreateForm({...createForm, smtpGroupId: e.target.value || null})} className={inputCls}>
                    <option value="">— Use default —</option>
                    {groups.map(g => <option key={g.id} value={g.id}>{g.name}{g.isDefault && ' (default)'}</option>)}
                  </select>
                </div>
              </div>
            </div>
            <div className="flex justify-end gap-2 mt-5">
              <button type="button" onClick={() => setCreating(false)} className="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-lg">Cancel</button>
              <button type="submit" disabled={saving} className="px-5 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50 font-medium">
                {saving ? 'Creating...' : 'Create User'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* ─── EDIT MODAL ─── */}
      {editing && (
        <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold text-gray-900">Edit User</h3>
              <button onClick={() => setEditing(null)} className="p-1 hover:bg-gray-100 rounded"><X className="w-5 h-5" /></button>
            </div>
            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Full Name *</label>
                <input type="text" value={editForm.fullName} onChange={e => setEditForm({...editForm, fullName: e.target.value})} className={inputCls} />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Email *</label>
                <input type="email" value={editForm.email} onChange={e => setEditForm({...editForm, email: e.target.value})} className={inputCls} />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">SMTP Group</label>
                <select value={editForm.smtpGroupId ?? ''} onChange={e => setEditForm({...editForm, smtpGroupId: e.target.value || null})} className={inputCls}>
                  <option value="">— Use default —</option>
                  {groups.map(g => <option key={g.id} value={g.id}>{g.name}{g.isDefault && ' (default)'}</option>)}
                </select>
              </div>
              <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
                <input type="checkbox" checked={editForm.isActive} onChange={e => setEditForm({...editForm, isActive: e.target.checked})} className="w-4 h-4 text-indigo-600 rounded" />
                Account active
              </label>
            </div>
            <div className="flex justify-end gap-2 mt-5">
              <button onClick={() => setEditing(null)} className="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-lg">Cancel</button>
              <button onClick={handleSaveEdit} disabled={saving} className="px-5 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 font-medium">
                {saving ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ─── RESET PASSWORD MODAL ─── */}
      {resettingUser && (
        <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold text-gray-900 flex items-center gap-2">
                <KeyRound className="w-5 h-5 text-amber-600" /> Reset Password
              </h3>
              <button onClick={() => setResettingUser(null)} className="p-1 hover:bg-gray-100 rounded"><X className="w-5 h-5" /></button>
            </div>
            <p className="text-sm text-gray-600 mb-3">
              Set a new password for <strong>{resettingUser.fullName}</strong> ({resettingUser.email}).
              Share the new password with them through a secure channel.
            </p>
            <input
              type="text"
              autoFocus
              value={resetPwValue}
              onChange={e => setResetPwValue(e.target.value)}
              placeholder="New password (min 8 chars)"
              className={inputCls + ' font-mono'}
            />
            <button
              type="button"
              onClick={() => {
                // Generate random secure password
                const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$';
                let pw = '';
                for (let i = 0; i < 14; i++) pw += chars[Math.floor(Math.random() * chars.length)];
                setResetPwValue(pw);
              }}
              className="mt-2 text-xs text-amber-700 hover:underline"
            >
              ✨ Generate random secure password
            </button>
            <div className="flex justify-end gap-2 mt-5">
              <button onClick={() => setResettingUser(null)} className="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-lg">Cancel</button>
              <button onClick={handleResetPassword} disabled={saving || resetPwValue.length < 4} className="px-5 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 disabled:opacity-50 font-medium">
                {saving ? 'Resetting...' : 'Reset Password'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
