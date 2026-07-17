import { cn } from '@/lib/utils'

type SourcingSourceLogoProps = {
  source: string
  className?: string
}

export function SourcingSourceLogo({
  source,
  className,
}: SourcingSourceLogoProps) {
  const normalizedSource = source.toLowerCase()
  if (normalizedSource !== 'boamp' && normalizedSource !== 'ted') return null

  return (
    <img
      src={`/images/sources/${normalizedSource}.svg`}
      alt=''
      className={cn('size-5 shrink-0 object-contain', className)}
    />
  )
}

export function BoampLogo({ className }: { className?: string }) {
  return <SourcingSourceLogo source='boamp' className={className} />
}

export function TedLogo({ className }: { className?: string }) {
  return <SourcingSourceLogo source='ted' className={className} />
}
