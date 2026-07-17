import { type SVGProps } from 'react'
import { cn } from '@/lib/utils'

function LogoMark() {
  return (
    <>
      <rect width='64' height='64' rx='14' fill='#172033' />
      <path
        fill='#fff'
        d='M17 13h20c8 0 13 5 13 12 0 5-3 9-8 11l10 15H42L33 38h-8v13h-8V13Zm8 8v10h11c4 0 6-2 6-5s-2-5-6-5H25Z'
      />
      <path fill='#e84d3d' d='m7 47 12-11v7h10l8 8H19v7L7 47Z' />
    </>
  )
}

export function Logo({ className, ...props }: SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox='0 0 64 64'
      xmlns='http://www.w3.org/2000/svg'
      className={cn('size-8', className)}
      {...props}
    >
      <title>RiposteOS</title>
      <LogoMark />
    </svg>
  )
}

export function LogoLockup({ className, ...props }: SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox='0 0 330 64'
      xmlns='http://www.w3.org/2000/svg'
      className={cn('h-8 w-auto', className)}
      {...props}
    >
      <title>RiposteOS</title>
      <LogoMark />
      <text
        x='82'
        y='43'
        fill='currentColor'
        fontFamily='Manrope, Inter, Arial, sans-serif'
        fontSize='38'
        fontWeight='750'
        letterSpacing='-1.5'
      >
        Riposte
        <tspan fill='#e84d3d'>OS</tspan>
      </text>
    </svg>
  )
}
