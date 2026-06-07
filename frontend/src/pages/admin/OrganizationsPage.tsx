import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Building2, Loader2, Plus, Users, ShieldCheck, Pencil, Trash2, Check, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { organizationsApi, type Organization } from '../../api/organizationsApi';
import { adminUsersApi, type AdminUser } from '../../api/adminUsersApi';
import { useAuthStore } from '../../store/authStore';

export default function OrganizationsPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const isAdmin = user?.role?.toLowerCase() === 'admin';

  const [orgs, setOrgs] = useState<Organization[]>([]);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState('');
  const [assignUserId, setAssignUserId] = useState('');
  const [assignOrgId, setAssignOrgId] = useState('');
  const [editId, setEditId] = useState<string | null>(null);
  const [editName, setEditName] = useState('');
  const [busyId, setBusyId] = useState<string | null>(null);

  useEffect(() => {
    if (!isAdmin) {
      toast.error('Admin access required');
      navigate('/dashboard');
    }
  }, [isAdmin, navigate]);

  const load = async () => {
    try {
      const [o, u]: any[] = await Promise.all([organizationsApi.list(), adminUsersApi.list({})]);
      setOrgs(o.data || []);
      setUsers(u.data || []);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to load organizations');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isAdmin) load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin]);

  const create = async () => {
    if (!newName.trim()) {
      toast.error('Enter an organization name');
      return;
    }
    setCreating(true);
    try {
      await organizationsApi.create({ name: newName.trim() });
      toast.success('Organization created');
      setNewName('');
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Create failed');
    } finally {
      setCreating(false);
    }
  };

  const assign = async () => {
    if (!assignUserId || !assignOrgId) {
      toast.error('Pick a user and an organization');
      return;
    }
    try {
      await organizationsApi.assignUser(assignUserId, assignOrgId);
      toast.success('User assigned');
      setAssignUserId('');
      setAssignOrgId('');
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Assign failed');
    }
  };

  const startEdit = (o: Organization) => { setEditId(o.id); setEditName(o.name); };
  const saveEdit = async (o: Organization) => {
    if (!editName.trim()) { toast.error('Name required'); return; }
    setBusyId(o.id);
    try {
      await organizationsApi.update(o.id, { name: editName.trim() });
      toast.success('Organization renamed');
      setEditId(null);
      await load();
    } catch (err: any) { toast.error(err?.response?.data?.message || 'Update failed'); }
    finally { setBusyId(null); }
  };
  const removeOrg = async (o: Organization) => {
    if (!window.confirm(`Delete "${o.name}"?${o.userCount > 0 ? ` Its ${o.userCount} member(s) will be moved back to the Legacy organization.` : ''}`)) return;
    setBusyId(o.id);
    try {
      await organizationsApi.remove(o.id);
      toast.success('Organization deleted');
      await load();
    } catch (err: any) { toast.error(err?.response?.data?.message || 'Delete failed'); }
    finally { setBusyId(null); }
  };

  if (!isAdmin) return null;
  if (loading) {
    return <div className="flex items-center justify-center h-64"><Loader2 className="w-6 h-6 animate-spin text-indigo-500" /></div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <div className="p-2.5 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl">
          <Building2 className="w-6 h-6 text-white" />
        </div>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Organizations</h1>
          <p className="text-gray-500 text-sm">Tenant grouping. Data isolation is not yet enforced &mdash; all data remains scoped per user until multi-tenancy is enabled.</p>
        </div>
      </div>

      {/* Create */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
        <h2 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2"><Plus className="w-4 h-4" /> New organization</h2>
        <div className="flex gap-2 flex-wrap">
          <input
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            placeholder="Organization name (e.g. Acme Realty)"
            className="flex-1 min-w-[220px] px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none"
            onKeyDown={(e) => e.key === 'Enter' && create()}
          />
          <button
            onClick={create}
            disabled={creating}
            className="px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50"
          >
            {creating ? 'Creating...' : 'Create'}
          </button>
        </div>
      </div>

      {/* List */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-gray-500 text-left">
            <tr>
              <th className="px-5 py-3 font-medium">Name</th>
              <th className="px-5 py-3 font-medium">Slug</th>
              <th className="px-5 py-3 font-medium">Plan</th>
              <th className="px-5 py-3 font-medium">Users</th>
              <th className="px-5 py-3 font-medium">Created</th>
              <th className="px-5 py-3 font-medium text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {orgs.map((o) => (
              <tr key={o.id} className="hover:bg-gray-50">
                <td className="px-5 py-3 font-medium text-gray-900">
                  {editId === o.id ? (
                    <div className="flex items-center gap-2">
                      <input value={editName} autoFocus onChange={(e) => setEditName(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && saveEdit(o)}
                        className="px-2 py-1 border border-gray-200 rounded text-sm focus:ring-2 focus:ring-indigo-400 outline-none" />
                      <button onClick={() => saveEdit(o)} disabled={busyId === o.id} className="text-green-600 hover:text-green-700" title="Save"><Check className="w-4 h-4" /></button>
                      <button onClick={() => setEditId(null)} className="text-gray-400 hover:text-gray-600" title="Cancel"><X className="w-4 h-4" /></button>
                    </div>
                  ) : (
                    <span className="flex items-center gap-2">
                      {o.name}
                      {o.isLegacy && (
                        <span className="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 border border-slate-200 inline-flex items-center gap-1">
                          <ShieldCheck className="w-3 h-3" /> Legacy
                        </span>
                      )}
                    </span>
                  )}
                </td>
                <td className="px-5 py-3 text-gray-500 font-mono text-xs">{o.slug}</td>
                <td className="px-5 py-3 capitalize text-gray-700">{o.planCode}</td>
                <td className="px-5 py-3 text-gray-700"><span className="inline-flex items-center gap-1"><Users className="w-3.5 h-3.5 text-gray-400" />{o.userCount}</span></td>
                <td className="px-5 py-3 text-gray-500">{new Date(o.createdAt).toLocaleDateString()}</td>
                <td className="px-5 py-3">
                  {o.isLegacy ? (
                    <span className="block text-right text-xs text-gray-300" title="The Legacy organization is protected">—</span>
                  ) : (
                    <div className="flex gap-1 justify-end">
                      <button onClick={() => startEdit(o)} title="Rename" className="p-1.5 text-gray-400 hover:text-amber-600 hover:bg-amber-50 rounded-lg"><Pencil className="w-4 h-4" /></button>
                      <button onClick={() => removeOrg(o)} disabled={busyId === o.id} title="Delete" className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg disabled:opacity-40">
                        {busyId === o.id ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
                      </button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
            {orgs.length === 0 && (
              <tr><td colSpan={6} className="px-5 py-8 text-center text-gray-400">No organizations yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Assign user */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
        <h2 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2"><Users className="w-4 h-4" /> Assign a user to an organization</h2>
        <div className="flex gap-2 flex-wrap">
          <select
            value={assignUserId}
            onChange={(e) => setAssignUserId(e.target.value)}
            className="flex-1 min-w-[200px] px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none"
          >
            <option value="">Select user…</option>
            {users.map((u) => (
              <option key={u.id} value={u.id}>{u.fullName} ({u.email})</option>
            ))}
          </select>
          <select
            value={assignOrgId}
            onChange={(e) => setAssignOrgId(e.target.value)}
            className="flex-1 min-w-[200px] px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none"
          >
            <option value="">Select organization…</option>
            {orgs.map((o) => (
              <option key={o.id} value={o.id}>{o.name}</option>
            ))}
          </select>
          <button
            onClick={assign}
            className="px-4 py-2 bg-gray-800 text-white rounded-lg text-sm font-medium hover:bg-gray-900"
          >
            Assign
          </button>
        </div>
      </div>
    </div>
  );
}
