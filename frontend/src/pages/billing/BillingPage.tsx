import { useEffect, useState } from 'react';
import { CreditCard, Check, Loader2, Mail, MessageSquare, Users, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';
import { billingApi, type Plan, type Subscription, type UsageMetric } from '../../api/billingApi';

export default function BillingPage() {
  const [plans, setPlans] = useState<Plan[]>([]);
  const [sub, setSub] = useState<Subscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [changing, setChanging] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const [p, s]: any[] = await Promise.all([billingApi.plans(), billingApi.subscription()]);
      setPlans(p.data || []);
      setSub(s.data || null);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to load billing');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const changePlan = async (code: string) => {
    if (code === sub?.planCode) return;
    setChanging(code);
    try {
      if (code === 'free') {
        // Downgrade to Free needs no payment.
        const res: any = await billingApi.changePlan(code);
        setSub(res.data);
        toast.success(res?.message || 'Switched to Free');
      } else {
        // Paid plan → checkout. Mock provider activates immediately; real providers redirect.
        const origin = window.location.origin;
        const res: any = await billingApi.checkout(
          code,
          `${origin}/billing?checkout=success`,
          `${origin}/billing?checkout=cancel`,
        );
        if (res.data?.activated) {
          toast.success(`Upgraded to ${code}`);
          await load();
        } else if (res.data?.checkoutUrl) {
          window.location.href = res.data.checkoutUrl;
          return;
        } else {
          toast.error('Checkout could not be started');
        }
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Plan change failed');
    } finally {
      setChanging(null);
    }
  };

  const fmtLimit = (n: number) => (n < 0 ? 'Unlimited' : n.toLocaleString());

  if (loading) {
    return <div className="flex items-center justify-center h-64"><Loader2 className="w-6 h-6 animate-spin text-indigo-500" /></div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <div className="p-2.5 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl">
          <CreditCard className="w-6 h-6 text-white" />
        </div>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Plan &amp; Usage</h1>
          <p className="text-gray-500 text-sm">Your subscription and this month's usage</p>
        </div>
      </div>

      {/* Current plan + usage */}
      {sub && (
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-6">
          <div className="flex items-center justify-between flex-wrap gap-2 mb-5">
            <div>
              <p className="text-sm text-gray-500">Current plan</p>
              <p className="text-xl font-bold text-gray-900">
                {sub.planName} {sub.priceAedMonthly > 0 && <span className="text-sm font-normal text-gray-500">· AED {sub.priceAedMonthly}/mo</span>}
              </p>
            </div>
            {!sub.quotasEnforced && (
              <span className="text-xs px-2 py-1 rounded-full bg-amber-50 text-amber-700 border border-amber-200">
                Quotas tracked, not enforced
              </span>
            )}
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <UsageBar icon={Users} label="Contacts" m={sub.contacts} />
            <UsageBar icon={Mail} label="Emails (mo)" m={sub.emails} />
            <UsageBar icon={MessageSquare} label="WhatsApp (mo)" m={sub.whatsApp} />
            <UsageBar icon={Sparkles} label="AI (mo)" m={sub.ai} />
          </div>
        </div>
      )}

      {/* Plans */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
        {plans.map(p => {
          const current = p.code === sub?.planCode;
          return (
            <div key={p.code} className={`rounded-2xl border p-5 flex flex-col ${current ? 'border-indigo-500 ring-2 ring-indigo-100' : 'border-gray-200'}`}>
              <p className="font-semibold text-gray-900">{p.name}</p>
              <p className="text-2xl font-bold text-gray-900 mt-1">
                {p.priceAedMonthly === 0 ? 'Free' : <>AED {p.priceAedMonthly}<span className="text-sm font-normal text-gray-400">/mo</span></>}
              </p>
              <ul className="text-xs text-gray-600 mt-3 space-y-1.5 flex-1">
                <li><Check className="w-3 h-3 inline text-green-500 mr-1" />{fmtLimit(p.maxContacts)} contacts</li>
                <li><Check className="w-3 h-3 inline text-green-500 mr-1" />{fmtLimit(p.maxEmailsPerMonth)} emails/mo</li>
                <li><Check className="w-3 h-3 inline text-green-500 mr-1" />{fmtLimit(p.maxWhatsAppPerMonth)} WhatsApp/mo</li>
                <li><Check className="w-3 h-3 inline text-green-500 mr-1" />{fmtLimit(p.maxAiPerMonth)} AI/mo</li>
                <li><Check className="w-3 h-3 inline text-green-500 mr-1" />{fmtLimit(p.maxUsers)} user(s)</li>
              </ul>
              <button
                onClick={() => changePlan(p.code)}
                disabled={current || changing === p.code}
                className={`mt-4 w-full py-2 rounded-lg text-sm font-medium ${current ? 'bg-gray-100 text-gray-400 cursor-default' : 'bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50'}`}
              >
                {changing === p.code ? <Loader2 className="w-4 h-4 animate-spin inline" /> : current ? 'Current plan' : 'Switch'}
              </button>
            </div>
          );
        })}
      </div>
      <p className="text-xs text-gray-400">Card payment (Stripe / Telr) is coming — plan switching here is manual for now.</p>
    </div>
  );
}

function UsageBar({ icon: Icon, label, m }: { icon: any; label: string; m: UsageMetric }) {
  const barColor = m.overLimit ? 'bg-red-500' : m.percent > 80 ? 'bg-amber-500' : 'bg-indigo-500';
  return (
    <div className="rounded-xl border border-gray-100 p-3">
      <div className="flex items-center gap-1.5 text-xs text-gray-500 mb-1"><Icon className="w-3.5 h-3.5" /> {label}</div>
      <p className="text-sm font-semibold text-gray-900">
        {m.used.toLocaleString()} <span className="text-gray-400 font-normal">/ {m.unlimited ? '∞' : m.limit.toLocaleString()}</span>
      </p>
      {!m.unlimited && (
        <div className="w-full h-1.5 bg-gray-100 rounded-full mt-2 overflow-hidden">
          <div className={`h-full ${barColor}`} style={{ width: `${Math.min(100, m.percent)}%` }} />
        </div>
      )}
    </div>
  );
}
