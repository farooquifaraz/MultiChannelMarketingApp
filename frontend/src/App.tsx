import { useEffect, lazy, Suspense } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuthStore } from './store/authStore';
import Layout from './components/layout/Layout';

const LoginPage = lazy(() => import('./pages/auth/LoginPage'));
const RegisterPage = lazy(() => import('./pages/auth/RegisterPage'));
const DashboardPage = lazy(() => import('./pages/dashboard/DashboardPage'));
const CampaignsPage = lazy(() => import('./pages/campaigns/CampaignsPage'));
const CampaignDetailPage = lazy(() => import('./pages/campaigns/CampaignDetailPage'));
const ContactsPage = lazy(() => import('./pages/contacts/ContactsPage'));
const TemplatesPage = lazy(() => import('./pages/templates/TemplatesPage'));
const SettingsPage = lazy(() => import('./pages/settings/SettingsPage'));
const SendMessagePage = lazy(() => import('./pages/messages/SendMessagePage'));
const SmtpGroupsPage = lazy(() => import('./pages/admin/SmtpGroupsPage'));
const AdminUsersPage = lazy(() => import('./pages/admin/AdminUsersPage'));
const UserProfilePage = lazy(() => import('./pages/profile/UserProfilePage'));
const AuditLogsPage = lazy(() => import('./pages/admin/AuditLogsPage'));
const ScheduledCampaignsPage = lazy(() => import('./pages/campaigns/ScheduledCampaignsPage'));
const InboxPage = lazy(() => import('./pages/inbox/InboxPage'));
const BillingPage = lazy(() => import('./pages/billing/BillingPage'));
const OrganizationsPage = lazy(() => import('./pages/admin/OrganizationsPage'));
const BannerStudioPage = lazy(() => import('./pages/creatives/BannerStudioPage'));
const ContentStudioPage = lazy(() => import('./pages/creatives/ContentStudioPage'));
const IntegrationsPage = lazy(() => import('./pages/admin/IntegrationsPage'));

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function LoadingFallback() {
  return (
    <div className="flex items-center justify-center h-screen">
      <div className="w-8 h-8 border-4 border-primary-500 border-t-transparent rounded-full animate-spin" />
    </div>
  );
}

export default function App() {
  const initialize = useAuthStore((s) => s.initialize);

  useEffect(() => {
    initialize();
  }, [initialize]);

  return (
    <Suspense fallback={<LoadingFallback />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="campaigns" element={<CampaignsPage />} />
          <Route path="campaigns/scheduled" element={<ScheduledCampaignsPage />} />
          <Route path="campaigns/:id" element={<CampaignDetailPage />} />
          <Route path="contacts" element={<ContactsPage />} />
          <Route path="templates" element={<TemplatesPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="send" element={<SendMessagePage />} />
          <Route path="admin/smtp-groups" element={<SmtpGroupsPage />} />
          <Route path="admin/users" element={<AdminUsersPage />} />
          <Route path="profile" element={<UserProfilePage />} />
          <Route path="admin/audit-logs" element={<AuditLogsPage />} />
          <Route path="admin/organizations" element={<OrganizationsPage />} />
          <Route path="creatives" element={<BannerStudioPage />} />
          <Route path="content" element={<ContentStudioPage />} />
          <Route path="admin/integrations" element={<IntegrationsPage />} />
          <Route path="inbox" element={<InboxPage />} />
          <Route path="billing" element={<BillingPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  );
}
