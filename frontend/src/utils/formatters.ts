export function formatDate(dateStr?: string): string {
  if (!dateStr) return '-';
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatNumber(num: number): string {
  return new Intl.NumberFormat('en-US').format(num);
}

export function getStatusColor(status: string): string {
  const colors: Record<string, string> = {
    draft: 'bg-gray-100 text-gray-700',
    queued: 'bg-yellow-100 text-yellow-700',
    running: 'bg-blue-100 text-blue-700',
    completed: 'bg-green-100 text-green-700',
    failed: 'bg-red-100 text-red-700',
    pending: 'bg-gray-100 text-gray-700',
    sent: 'bg-green-100 text-green-700',
    delivered: 'bg-emerald-100 text-emerald-700',
    opened: 'bg-purple-100 text-purple-700',
  };
  return colors[status.toLowerCase()] || 'bg-gray-100 text-gray-700';
}

export function getChannelColor(channel: string): string {
  const colors: Record<string, string> = {
    email: 'bg-blue-100 text-blue-700',
    whatsapp: 'bg-green-100 text-green-700',
    sms: 'bg-purple-100 text-purple-700',
  };
  return colors[channel.toLowerCase()] || 'bg-gray-100 text-gray-700';
}

export function getChannelIcon(channel: string): string {
  const icons: Record<string, string> = {
    email: 'Mail',
    whatsapp: 'MessageCircle',
    sms: 'Smartphone',
  };
  return icons[channel.toLowerCase()] || 'Send';
}

export function cn(...classes: (string | undefined | false)[]): string {
  return classes.filter(Boolean).join(' ');
}
