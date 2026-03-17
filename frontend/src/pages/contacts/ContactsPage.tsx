import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, Upload, Trash2, Users, FolderPlus, X, Check, UserPlus, Edit3 } from 'lucide-react';
import { contactApi } from '../../api/contactApi';
import { formatDate } from '../../utils/formatters';
import toast from 'react-hot-toast';

export default function ContactsPage() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [groupFilter, setGroupFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [showImport, setShowImport] = useState(false);
  const [showCreateGroup, setShowCreateGroup] = useState(false);
  const [selectedContacts, setSelectedContacts] = useState<string[]>([]);
  const [assignGroupId, setAssignGroupId] = useState('');
  const [showAssign, setShowAssign] = useState(false);
  const [editingContact, setEditingContact] = useState<any>(null);

  // Contact form
  const [formName, setFormName] = useState('');
  const [formEmail, setFormEmail] = useState('');
  const [formPhone, setFormPhone] = useState('');
  const [formWhatsApp, setFormWhatsApp] = useState('');
  const [formGroupId, setFormGroupId] = useState('');

  // Group form
  const [groupName, setGroupName] = useState('');
  const [groupDesc, setGroupDesc] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['contacts', page, groupFilter, search],
    queryFn: () => contactApi.getAll({ pageNumber: page, pageSize: 20, groupId: groupFilter || undefined, search: search || undefined }),
  });

  const { data: groups } = useQuery({
    queryKey: ['contact-groups'],
    queryFn: () => contactApi.getGroups(),
  });

  const createMutation = useMutation({
    mutationFn: (d: any) => contactApi.create(d),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['contacts'] });
      queryClient.invalidateQueries({ queryKey: ['contact-groups'] });
      setShowCreate(false);
      resetForm();
      toast.success('Contact created');
    },
    onError: (err: any) => {
      toast.error(err?.response?.data?.message || 'Failed to create contact');
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: any }) => contactApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['contacts'] });
      queryClient.invalidateQueries({ queryKey: ['contact-groups'] });
      setEditingContact(null);
      resetForm();
      toast.success('Contact updated');
    },
    onError: (err: any) => {
      toast.error(err?.response?.data?.message || 'Failed to update contact');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => contactApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['contacts'] });
      queryClient.invalidateQueries({ queryKey: ['contact-groups'] });
      toast.success('Contact deleted');
    },
  });

  const createGroupMutation = useMutation({
    mutationFn: (d: any) => contactApi.createGroup(d),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['contact-groups'] });
      setShowCreateGroup(false);
      setGroupName(''); setGroupDesc('');
      toast.success('Group created');
    },
  });

  const importMutation = useMutation({
    mutationFn: ({ file, groupId }: { file: File; groupId?: string }) => contactApi.import(file, groupId),
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ['contacts'] });
      queryClient.invalidateQueries({ queryKey: ['contact-groups'] });
      setShowImport(false);
      toast.success(`Imported ${res.data.successCount} of ${res.data.totalRows} contacts`);
    },
    onError: (err: any) => {
      toast.error(err?.response?.data?.message || 'Import failed');
    },
  });

  const assignMutation = useMutation({
    mutationFn: ({ contactIds, groupId }: { contactIds: string[]; groupId: string | null }) =>
      contactApi.assignToGroup(contactIds, groupId),
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ['contacts'] });
      queryClient.invalidateQueries({ queryKey: ['contact-groups'] });
      setSelectedContacts([]);
      setShowAssign(false);
      setAssignGroupId('');
      toast.success(`${res.data.assignedCount} contacts assigned to group`);
    },
    onError: () => {
      toast.error('Failed to assign contacts to group');
    },
  });

  const resetForm = () => {
    setFormName(''); setFormEmail(''); setFormPhone(''); setFormWhatsApp(''); setFormGroupId('');
  };

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput);
    setPage(1);
  };

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    createMutation.mutate({
      fullName: formName,
      email: formEmail || undefined,
      phone: formPhone || undefined,
      whatsAppNumber: formWhatsApp || undefined,
      groupId: formGroupId || undefined,
    });
  };

  const handleUpdate = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingContact) return;
    updateMutation.mutate({
      id: editingContact.id,
      data: {
        fullName: formName,
        email: formEmail || undefined,
        phone: formPhone || undefined,
        whatsAppNumber: formWhatsApp || undefined,
        groupId: formGroupId || undefined,
      },
    });
  };

  const startEdit = (c: any) => {
    setEditingContact(c);
    setFormName(c.fullName);
    setFormEmail(c.email || '');
    setFormPhone(c.phone || '');
    setFormWhatsApp(c.whatsAppNumber || '');
    setFormGroupId(c.groupId || '');
    setShowCreate(false);
  };

  const cancelEdit = () => {
    setEditingContact(null);
    resetForm();
  };

  const handleImport = (e: React.FormEvent) => {
    e.preventDefault();
    const form = e.target as HTMLFormElement;
    const fileInput = form.querySelector('input[type="file"]') as HTMLInputElement;
    const file = fileInput.files?.[0];
    if (file) importMutation.mutate({ file, groupId: formGroupId || undefined });
  };

  const toggleSelect = (id: string) => {
    setSelectedContacts(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]);
  };

  const toggleSelectAll = () => {
    const contacts = data?.data || [];
    if (selectedContacts.length === contacts.length) {
      setSelectedContacts([]);
    } else {
      setSelectedContacts(contacts.map((c: any) => c.id));
    }
  };

  const handleAssign = () => {
    assignMutation.mutate({
      contactIds: selectedContacts,
      groupId: assignGroupId || null,
    });
  };

  const contacts = data?.data || [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Contacts</h1>
          <p className="text-gray-500 mt-1">Manage your contact database</p>
        </div>
        <div className="flex gap-2">
          <button onClick={() => setShowImport(!showImport)} className="flex items-center gap-2 px-4 py-2.5 border border-gray-200 rounded-xl text-sm font-medium hover:bg-gray-50">
            <Upload className="w-4 h-4" />
            Import CSV
          </button>
          <button onClick={() => setShowCreateGroup(!showCreateGroup)} className="flex items-center gap-2 px-4 py-2.5 border border-gray-200 rounded-xl text-sm font-medium hover:bg-gray-50">
            <FolderPlus className="w-4 h-4" />
            New Group
          </button>
          <button onClick={() => { setShowCreate(!showCreate); setEditingContact(null); resetForm(); }} className="flex items-center gap-2 px-4 py-2.5 bg-gradient-to-r from-primary-600 to-primary-700 text-white rounded-xl font-medium hover:from-primary-700 hover:to-primary-800 shadow-lg shadow-primary-200">
            <Plus className="w-5 h-5" />
            Add Contact
          </button>
        </div>
      </div>

      {/* Groups Filter */}
      {groups?.data && groups.data.length > 0 && (
        <div className="flex gap-2 flex-wrap">
          <button onClick={() => { setGroupFilter(''); setPage(1); }} className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-all ${!groupFilter ? 'bg-primary-100 text-primary-700' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}>
            All
          </button>
          {groups.data.map((g) => (
            <button key={g.id} onClick={() => { setGroupFilter(g.id); setPage(1); }} className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-all ${groupFilter === g.id ? 'bg-primary-100 text-primary-700' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}>
              {g.name} ({g.contactCount})
            </button>
          ))}
        </div>
      )}

      {/* Selected Actions Bar */}
      {selectedContacts.length > 0 && (
        <div className="bg-primary-50 border border-primary-200 rounded-xl p-4 flex items-center justify-between">
          <span className="text-sm font-medium text-primary-700">
            <Check className="w-4 h-4 inline mr-1" />
            {selectedContacts.length} contact{selectedContacts.length > 1 ? 's' : ''} selected
          </span>
          <div className="flex items-center gap-3">
            {!showAssign ? (
              <button onClick={() => setShowAssign(true)} className="flex items-center gap-2 px-4 py-2 bg-primary-600 text-white rounded-lg text-sm font-medium hover:bg-primary-700">
                <UserPlus className="w-4 h-4" />
                Assign to Group
              </button>
            ) : (
              <div className="flex items-center gap-2">
                <select
                  value={assignGroupId}
                  onChange={(e) => setAssignGroupId(e.target.value)}
                  className="px-3 py-2 border border-primary-200 rounded-lg text-sm bg-white focus:ring-2 focus:ring-primary-500 outline-none"
                >
                  <option value="">Remove from group</option>
                  {groups?.data?.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
                </select>
                <button
                  onClick={handleAssign}
                  disabled={assignMutation.isPending}
                  className="px-4 py-2 bg-green-600 text-white rounded-lg text-sm font-medium hover:bg-green-700 disabled:opacity-50"
                >
                  {assignMutation.isPending ? 'Assigning...' : 'Confirm'}
                </button>
                <button onClick={() => { setShowAssign(false); setAssignGroupId(''); }} className="p-2 text-gray-400 hover:text-gray-600">
                  <X className="w-4 h-4" />
                </button>
              </div>
            )}
            <button onClick={() => setSelectedContacts([])} className="text-sm text-gray-500 hover:text-gray-700 underline">
              Clear selection
            </button>
          </div>
        </div>
      )}

      {/* Create Group Form */}
      {showCreateGroup && (
        <div className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100">
          <h3 className="text-lg font-semibold mb-4">Create Group</h3>
          <form onSubmit={(e) => { e.preventDefault(); createGroupMutation.mutate({ name: groupName, description: groupDesc || undefined }); }} className="flex gap-4 items-end">
            <div className="flex-1">
              <label className="block text-sm font-medium text-gray-700 mb-1">Name</label>
              <input type="text" value={groupName} onChange={(e) => setGroupName(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none" required />
            </div>
            <div className="flex-1">
              <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
              <input type="text" value={groupDesc} onChange={(e) => setGroupDesc(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none" />
            </div>
            <button type="submit" className="px-6 py-2.5 bg-primary-600 text-white rounded-xl font-medium hover:bg-primary-700">Create</button>
            <button type="button" onClick={() => setShowCreateGroup(false)} className="p-2.5 text-gray-400 hover:text-gray-600"><X className="w-5 h-5" /></button>
          </form>
        </div>
      )}

      {/* Import Form */}
      {showImport && (
        <div className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100">
          <h3 className="text-lg font-semibold mb-4">Import Contacts</h3>
          <form onSubmit={handleImport} className="flex gap-4 items-end">
            <div className="flex-1">
              <label className="block text-sm font-medium text-gray-700 mb-1">CSV File</label>
              <input type="file" accept=".csv,.xlsx" className="w-full px-4 py-2 border border-gray-200 rounded-xl" required />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Group (optional)</label>
              <select value={formGroupId} onChange={(e) => setFormGroupId(e.target.value)} className="px-4 py-2.5 border border-gray-200 rounded-xl">
                <option value="">No group</option>
                {groups?.data?.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
              </select>
            </div>
            <button type="submit" disabled={importMutation.isPending} className="px-6 py-2.5 bg-primary-600 text-white rounded-xl font-medium hover:bg-primary-700 disabled:opacity-50">
              {importMutation.isPending ? 'Importing...' : 'Import'}
            </button>
          </form>
        </div>
      )}

      {/* Create / Edit Contact Form */}
      {(showCreate || editingContact) && (
        <div className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100">
          <h3 className="text-lg font-semibold mb-4">{editingContact ? 'Edit Contact' : 'Add Contact'}</h3>
          <form onSubmit={editingContact ? handleUpdate : handleCreate} className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Full Name *</label>
              <input type="text" value={formName} onChange={(e) => setFormName(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none" required />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
              <input type="email" value={formEmail} onChange={(e) => setFormEmail(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none" />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
              <input type="text" value={formPhone} onChange={(e) => setFormPhone(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none" />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">WhatsApp</label>
              <input type="text" value={formWhatsApp} onChange={(e) => setFormWhatsApp(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none" />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Group</label>
              <select value={formGroupId} onChange={(e) => setFormGroupId(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none bg-white">
                <option value="">No group</option>
                {groups?.data?.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
              </select>
            </div>
            <div className="flex items-end gap-3">
              <button type="button" onClick={editingContact ? cancelEdit : () => setShowCreate(false)} className="px-4 py-2.5 text-gray-600 hover:bg-gray-100 rounded-xl">Cancel</button>
              <button type="submit" disabled={createMutation.isPending || updateMutation.isPending} className="px-6 py-2.5 bg-primary-600 text-white rounded-xl font-medium hover:bg-primary-700 disabled:opacity-50">
                {(createMutation.isPending || updateMutation.isPending) ? 'Saving...' : editingContact ? 'Update' : 'Add Contact'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Search */}
      <form onSubmit={handleSearch} className="flex gap-3">
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
          <input
            type="text"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Search contacts..."
            className="w-full pl-10 pr-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none"
          />
        </div>
        <button type="submit" className="px-4 py-2 bg-gray-100 rounded-xl text-sm font-medium hover:bg-gray-200">Search</button>
      </form>

      {/* Contact List */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        {isLoading ? (
          <div className="p-12 text-center text-gray-400">
            <div className="w-8 h-8 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          </div>
        ) : contacts.length === 0 ? (
          <div className="p-12 text-center text-gray-400">
            <Users className="w-10 h-10 mx-auto mb-3 opacity-50" />
            <p className="text-lg font-medium">No contacts found</p>
          </div>
        ) : (
          <table className="w-full">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-100">
                <th className="text-left px-4 py-3 w-10">
                  <input
                    type="checkbox"
                    checked={contacts.length > 0 && selectedContacts.length === contacts.length}
                    onChange={toggleSelectAll}
                    className="w-4 h-4 text-primary-600 rounded border-gray-300 focus:ring-primary-500"
                  />
                </th>
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Name</th>
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Email</th>
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Phone</th>
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Group</th>
                <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Added</th>
                <th className="text-right px-4 py-3 text-xs font-semibold text-gray-500 uppercase">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {contacts.map((c: any) => (
                <tr key={c.id} className={`hover:bg-gray-50 ${selectedContacts.includes(c.id) ? 'bg-primary-50/50' : ''}`}>
                  <td className="px-4 py-3">
                    <input
                      type="checkbox"
                      checked={selectedContacts.includes(c.id)}
                      onChange={() => toggleSelect(c.id)}
                      className="w-4 h-4 text-primary-600 rounded border-gray-300 focus:ring-primary-500"
                    />
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 bg-primary-100 rounded-lg flex items-center justify-center text-sm font-semibold text-primary-600">
                        {c.fullName.charAt(0).toUpperCase()}
                      </div>
                      <span className="font-medium text-gray-900 text-sm">{c.fullName}</span>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-600">{c.email || '-'}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">{c.phone || '-'}</td>
                  <td className="px-4 py-3">
                    {c.groupName ? (
                      <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-700">
                        {c.groupName}
                      </span>
                    ) : (
                      <span className="text-sm text-gray-400">—</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-500">{formatDate(c.createdAt)}</td>
                  <td className="px-4 py-3 text-right">
                    <div className="flex items-center justify-end gap-1">
                      <button onClick={() => startEdit(c)} className="p-2 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg" title="Edit">
                        <Edit3 className="w-4 h-4" />
                      </button>
                      <button onClick={() => deleteMutation.mutate(c.id)} className="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg" title="Delete">
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between px-6 py-4 border-t border-gray-100">
            <p className="text-sm text-gray-500">Page {page} of {data.totalPages} ({data.totalCount} total)</p>
            <div className="flex gap-2">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={!data.hasPrevious} className="px-3 py-1.5 border rounded-lg text-sm disabled:opacity-50 hover:bg-gray-50">Previous</button>
              <button onClick={() => setPage(p => p + 1)} disabled={!data.hasNext} className="px-3 py-1.5 border rounded-lg text-sm disabled:opacity-50 hover:bg-gray-50">Next</button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
