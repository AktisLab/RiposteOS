import { findSourcingSource } from '@/lib/sourcing-source'
import { cn } from '@/lib/utils'

type SourcingSourceLogoProps = {
  source: string
  className?: string
}

export function SourcingSourceLogo({
  source,
  className,
}: SourcingSourceLogoProps) {
  const sourcingSource = findSourcingSource(source)
  if (!sourcingSource) return null

  return (
    <img
      src={`/images/sources/${sourcingSource.value}.svg`}
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

export function PlaceLogo({ className }: { className?: string }) {
  return <SourcingSourceLogo source='place' className={className} />
}
