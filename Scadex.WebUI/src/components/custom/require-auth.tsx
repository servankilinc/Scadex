import { Navigate, Outlet, useLocation } from 'react-router';
import { useAccessToken, useCurrentUser, usePermission } from '@/lib/auth-session';

interface RequireAuthProps {
  /** Verilirse kullanicinin bu izne de sahip olmasi gerekir (Faz 2). */
  permission?: string;
}

export default function RequireAuth({ permission }: RequireAuthProps) {
  const location = useLocation();
  const token = useAccessToken();
  const currentUser = useCurrentUser();
  const can = usePermission();

  // Oturum localStorage'dan aninda okunur: token, kullanici, rol ve izinlerin
  // tamami girise verilir; arkada tazeleyecek bir profil ucu YOKTUR. Bu yuzden
  // bekleme/skeleton yok. Token gecersizse ilk istekte 401 doner, interceptor
  // clearSession() yapar, store degisir ve bu bilesen yeniden render olup
  // asagidaki yonlendirmeye duser.
  if (!token || !currentUser) {
    // from: giristen sonra kullaniciyi geldigi sayfaya geri gonderebilmek icin.
    return <Navigate to='/login' replace state={{ from: location.pathname + location.search }} />;
  }

  if (permission && !can(permission)) {
    return <Navigate to='/' replace />;
  }

  return <Outlet />;
}
