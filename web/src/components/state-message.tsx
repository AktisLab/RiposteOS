import { cn } from '@/lib/utils'

type StateMessageProps = {
  icon: React.ReactNode
  children: React.ReactNode
  className?: string
  role?: 'status' | 'alert'
}

export function StateMessage({
  icon,
  children,
  className,
  role,
}: StateMessageProps) {
  return (
    <div
      role={role}
      className={cn(
        'flex min-h-64 flex-col items-center justify-center gap-3 px-6 text-center text-sm text-muted-foreground',
        className
      )}
    >
      <div className='rounded-full border bg-muted/40 p-3 [&_svg]:size-5'>
        {icon}
      </div>
      <p>{children}</p>
    </div>
  )
}
