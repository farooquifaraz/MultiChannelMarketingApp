import { useQuery } from '@tanstack/react-query';
import {
  Users,
  Send,
  CheckCircle,
  Activity,
  TrendingUp,
  Mail,
  MessageCircle,
  Smartphone,
} from 'lucide-react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
} from 'recharts';
import { dashboardApi } from '../../api/dashboardApi';
import { formatDate, formatNumber, getStatusColor, getChannelColor } from '../../utils/formatters';
import { Link } from 'react-router-dom';

const COLORS = ['#3b82f6', '#22c55e', '#a855f7'];

export default function DashboardPage() {
  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ['dashboard-stats'],
    queryFn: () => dashboardApi.getStats(),
  });

  const { data: recentCampaigns } = useQuery({
    queryKey: ['recent-campaigns'],
    queryFn: () => dashboardApi.getRecentCampaigns(5),
  });

  const { data: channelBreakdown } = useQuery({
    queryKey: ['channel-breakdown'],
    queryFn: () => dashboardApi.getChannelBreakdown(),
  });

  const statCards = [
    {
      label: 'Total Contacts',
      value: stats?.data?.totalContacts || 0,
      icon: Users,
      color: 'from-blue-500 to-blue-600',
      bgColor: 'bg-blue-50',
      iconColor: 'text-blue-600',
    },
    {
      label: 'Total Campaigns',
      value: stats?.data?.totalCampaigns || 0,
      icon: Send,
      color: 'from-purple-500 to-purple-600',
      bgColor: 'bg-purple-50',
      iconColor: 'text-purple-600',
    },
    {
      label: 'Messages Sent',
      value: stats?.data?.totalMessagesSent || 0,
      icon: CheckCircle,
      color: 'from-green-500 to-green-600',
      bgColor: 'bg-green-50',
      iconColor: 'text-green-600',
    },
    {
      label: 'Success Rate',
      value: `${stats?.data?.overallSuccessRate || 0}%`,
      icon: TrendingUp,
      color: 'from-amber-500 to-amber-600',
      bgColor: 'bg-amber-50',
      iconColor: 'text-amber-600',
    },
  ];

  const channelData = channelBreakdown?.data
    ? [
        { name: 'Email', sent: channelBreakdown.data.email.messagesSent, failed: channelBreakdown.data.email.messagesFailed, campaigns: channelBreakdown.data.email.campaignCount },
        { name: 'WhatsApp', sent: channelBreakdown.data.whatsApp.messagesSent, failed: channelBreakdown.data.whatsApp.messagesFailed, campaigns: channelBreakdown.data.whatsApp.campaignCount },
        { name: 'SMS', sent: channelBreakdown.data.sms.messagesSent, failed: channelBreakdown.data.sms.messagesFailed, campaigns: channelBreakdown.data.sms.campaignCount },
      ]
    : [];

  const pieData = channelData.filter(d => d.campaigns > 0).map(d => ({ name: d.name, value: d.campaigns }));

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
        <p className="text-gray-500 mt-1">Overview of your marketing performance</p>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {statCards.map((card) => (
          <div key={card.label} className="bg-white rounded-2xl p-5 shadow-sm border border-gray-100 hover:shadow-md transition-shadow">
            <div className={`w-11 h-11 rounded-xl bg-gradient-to-br ${card.color} grid place-items-center shadow-md mb-3`}>
              <card.icon className="w-5 h-5 text-white" />
            </div>
            <p className="text-[26px] font-extrabold tracking-tight text-gray-900 leading-tight">
              {statsLoading ? '…' : typeof card.value === 'number' ? formatNumber(card.value) : card.value}
            </p>
            <p className="text-[13px] text-gray-500 mt-0.5">{card.label}</p>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {/* Channel Bar Chart */}
        <div className="lg:col-span-2 bg-white rounded-2xl p-6 shadow-sm border border-gray-100">
          <h3 className="text-lg font-semibold text-gray-900 mb-4">Channel Performance</h3>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={channelData}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
              <XAxis dataKey="name" stroke="#64748b" fontSize={13} />
              <YAxis stroke="#64748b" fontSize={13} />
              <Tooltip
                contentStyle={{ borderRadius: '12px', border: '1px solid #e2e8f0' }}
              />
              <Bar dataKey="sent" name="Sent" fill="#22c55e" radius={[6, 6, 0, 0]} />
              <Bar dataKey="failed" name="Failed" fill="#ef4444" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        {/* Pie Chart */}
        <div className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100">
          <h3 className="text-lg font-semibold text-gray-900 mb-4">Campaign Distribution</h3>
          {pieData.length > 0 ? (
            <ResponsiveContainer width="100%" height={250}>
              <PieChart>
                <Pie
                  data={pieData}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={100}
                  paddingAngle={5}
                  dataKey="value"
                >
                  {pieData.map((_, index) => (
                    <Cell key={index} fill={COLORS[index % COLORS.length]} />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
          ) : (
            <div className="flex items-center justify-center h-[250px] text-gray-400">
              No campaign data yet
            </div>
          )}
          <div className="flex justify-center gap-4 mt-2">
            {[
              { icon: Mail, label: 'Email', color: 'text-blue-500' },
              { icon: MessageCircle, label: 'WhatsApp', color: 'text-green-500' },
              { icon: Smartphone, label: 'SMS', color: 'text-purple-500' },
            ].map((ch) => (
              <div key={ch.label} className="flex items-center gap-1.5 text-sm text-gray-600">
                <ch.icon className={`w-4 h-4 ${ch.color}`} />
                {ch.label}
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Recent Campaigns */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100">
        <div className="p-6 border-b border-gray-100 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-gray-900">Recent Campaigns</h3>
          <Link to="/campaigns" className="text-sm text-primary-600 font-medium hover:text-primary-700">
            View all
          </Link>
        </div>
        <div className="divide-y divide-gray-50">
          {recentCampaigns?.data?.length ? (
            recentCampaigns.data.map((c) => (
              <Link
                key={c.id}
                to={`/campaigns/${c.id}`}
                className="flex items-center justify-between p-4 px-6 hover:bg-gray-50 transition-colors"
              >
                <div className="flex items-center gap-4">
                  <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${getChannelColor(c.channel)}`}>
                    {c.channel === 'email' ? <Mail className="w-5 h-5" /> : c.channel === 'whatsapp' ? <MessageCircle className="w-5 h-5" /> : <Smartphone className="w-5 h-5" />}
                  </div>
                  <div>
                    <p className="font-medium text-gray-900">{c.name}</p>
                    <p className="text-sm text-gray-500">{formatDate(c.createdAt)}</p>
                  </div>
                </div>
                <div className="flex items-center gap-4">
                  <div className="text-right">
                    <p className="text-sm font-medium text-gray-900">{formatNumber(c.sentCount)}/{formatNumber(c.totalContacts)}</p>
                    <p className="text-xs text-gray-500">sent</p>
                  </div>
                  <span className={`px-2.5 py-1 rounded-lg text-xs font-medium ${getStatusColor(c.status)}`}>
                    {c.status}
                  </span>
                </div>
              </Link>
            ))
          ) : (
            <div className="p-8 text-center text-gray-400">
              <Activity className="w-8 h-8 mx-auto mb-2 opacity-50" />
              No campaigns yet. Create your first campaign!
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
