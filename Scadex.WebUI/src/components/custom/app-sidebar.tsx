import type * as React from 'react';
import { Link, useLocation, useNavigate } from 'react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Building2Icon,
  CameraIcon,
  CheckIcon,
  ChevronsUpDown,
  CpuIcon,
  HistoryIcon,
  LogOut,
  MonitorIcon,
  MoonIcon,
  SettingsIcon,
  ShapesIcon,
  SunIcon,
  VideoIcon,
  Zap,
  type LucideIcon
} from 'lucide-react';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar
} from '@/components/ui/sidebar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { useCurrentUser } from '@/lib/auth-session';
import { logout } from '@/api/auth';
import { useAppDispatch, useAppSelector } from '@/hooks';
import { setTheme, type Theme } from '@/store/reducers/themeSlice';

/**
 * Uygulama kenar çubuğu.
 *
 * shadcn şablonundan gelen demo verisi (Acme Inc / Enterprise, Playground /
 * History / Starred, Support / Feedback, Upgrade to Pro, Account, Billing,
 * Notifications) TAMAMEN kaldırıldı. Hepsi `url: '#'` ile ham `<a href>`
 * kullanıyordu; bu hem hiçbir yere gitmiyor hem de router'ı baypas edip tam
 * sayfa yenilemesi yapıyordu.
 *
 * Buraya yalnızca GERÇEK hedefler eklenir — boş bir sayfaya götüren bağlantı,
 * olmayan bağlantıdan kötüdür. `/admin` kendisi hâlâ boş olduğu için listede
 * yok; yalnızca gerçekten çalışan `/admin/templates` var.
 */

interface NavItem {
  title: string;
  url: string;
  icon: LucideIcon;
}

const NAV_ITEMS: NavItem[] = [
  { title: 'Kabinler', url: '/cabinets', icon: CpuIcon },
  { title: 'Olay Geçmişi', url: '/events', icon: HistoryIcon },
  // Canlı izleme, kamera TANIMINDAN ayrı bir madde: biri izlemek, diğeri
  // (/admin/cameras) tanımlamak için.
  { title: 'Canlı İzleme', url: '/cameras', icon: VideoIcon },
  { title: 'Firmalar', url: '/admin/companies', icon: Building2Icon },
  { title: 'Şablonlar', url: '/admin/templates', icon: ShapesIcon },
  { title: 'Kameralar', url: '/admin/cameras', icon: CameraIcon },
  { title: 'Ayarlar', url: '/admin/settings', icon: SettingsIcon }
];

export function AppSidebar({ ...props }: React.ComponentProps<typeof Sidebar>) {
  return (
    <Sidebar variant='inset' {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size='lg' render={<Link to='/' />}>
              <div className='bg-sidebar-primary text-sidebar-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg'>
                <Zap className='size-4' />
              </div>
              <div className='grid flex-1 text-left text-sm leading-tight'>
                <span className='truncate font-medium'>Scadex</span>
                <span className='truncate text-xs'>Diyagram & SCADA</span>
              </div>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        <MainNav />
      </SidebarContent>

      <SidebarFooter>
        <NavUser />
      </SidebarFooter>
    </Sidebar>
  );
}

function MainNav() {
  const { pathname } = useLocation();

  return (
    <SidebarGroup>
      <SidebarGroupLabel>Uygulama</SidebarGroupLabel>
      <SidebarMenu>
        {NAV_ITEMS.map(item => (
          <SidebarMenuItem key={item.url}>
            <SidebarMenuButton
              tooltip={item.title}
              // startsWith: /cabinets/:id/diagram icindeyken de "Kabinler" aktif kalir.
              isActive={pathname === item.url || pathname.startsWith(`${item.url}/`)}
              render={<Link to={item.url} />}>
              <item.icon />
              <span>{item.title}</span>
            </SidebarMenuButton>
          </SidebarMenuItem>
        ))}
      </SidebarMenu>
    </SidebarGroup>
  );
}

const THEME_OPTIONS: { value: Theme; label: string; icon: LucideIcon }[] = [
  { value: 'light', label: 'Açık', icon: SunIcon },
  { value: 'dark', label: 'Koyu', icon: MoonIcon },
  { value: 'system', label: 'Sistem', icon: MonitorIcon }
];

function NavUser() {
  const { isMobile } = useSidebar();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const currentUser = useCurrentUser();
  const dispatch = useAppDispatch();
  const activeTheme = useAppSelector(s => s.theme.activeTheme);

  // UserBaseDto yalnizca id/fullName/companyId tasir; e-posta oturum yanitinda
  // gelmedigi icin hic gosterilmez.
  const name = currentUser?.fullName ?? '';
  const initials =
    name
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map(part => part[0]?.toUpperCase() ?? '')
      .join('') || '?';

  const logoutMutation = useMutation({
    mutationFn: logout,
    // logout() sunucu hatasinda bile yerel oturumu temizler; bu yuzden
    // basari/hata ayrimi yapmadan her iki durumda da login'e gidilir.
    onSettled: () => {
      queryClient.clear();
      navigate('/login', { replace: true });
    }
  });

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <SidebarMenuButton size='lg' className='data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground'>
                <Avatar className='size-8 rounded-lg'>
                  <AvatarFallback className='rounded-lg'>{initials}</AvatarFallback>
                </Avatar>
                <div className='grid flex-1 text-left text-sm leading-tight'>
                  <span className='truncate font-medium'>{name}</span>
                </div>
                <ChevronsUpDown className='ml-auto size-4' />
              </SidebarMenuButton>
            }
          />
          <DropdownMenuContent className='min-w-56 rounded-lg' side={isMobile ? 'bottom' : 'right'} align='end' sideOffset={4}>
            {/* Duz <div>: DropdownMenuLabel = Base UI `Menu.GroupLabel` ve bir
                GRUBU etiketlemek zorunda (Radix'in serbest Label'i degil).
                Kullanici adi bir grubun basligi degil, menunun basligi. */}
            <div className='text-muted-foreground truncate px-1.5 py-1 text-xs font-medium'>{name}</div>
            <DropdownMenuSeparator />

            {/* Tema secimi Redux'ta duruyordu ama onu degistirecek HICBIR arayuz
                yoktu; menudeki olu maddelerin yerini gercek bir islev aldi. */}
            <DropdownMenuGroup>
              <DropdownMenuLabel>Tema</DropdownMenuLabel>
              {THEME_OPTIONS.map(option => (
                <DropdownMenuItem key={option.value} onClick={() => dispatch(setTheme(option.value))}>
                  <option.icon />
                  {option.label}
                  {activeTheme === option.value && <CheckIcon className='ml-auto size-4' />}
                </DropdownMenuItem>
              ))}
            </DropdownMenuGroup>

            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => logoutMutation.mutate()} disabled={logoutMutation.isPending}>
              <LogOut />
              {logoutMutation.isPending ? 'Çıkış yapılıyor…' : 'Çıkış yap'}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}
