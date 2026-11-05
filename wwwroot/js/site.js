/* CONFIG */
const API = '';  // same origin
let currentPage = 'home';
let currentProductId = null;
let currentDetailQty = 1;
let selectedPayment = 'cod';
let selectedRating  = 0;
let activeCategory  = '';
let cartItems = [];   // local cart cache

/* AUTH STATE */
let authToken = localStorage.getItem('token') || null;
let authUser  = JSON.parse(localStorage.getItem('user') || 'null');

function isLoggedIn() { return !!authToken; }
function isAdmin()    { return authUser?.role === 'admin'; }

function updateNavAuth() {
    const loggedIn = isLoggedIn();
    document.getElementById('nav-auth-btn').style.display = loggedIn ? 'none' : '';
    document.getElementById('nav-user-btn').style.display = loggedIn ? '' : 'none';
    document.getElementById('nav-orders-btn').style.display = loggedIn ? '' : 'none';
    document.getElementById('nav-admin-btn').style.display  = (loggedIn && isAdmin()) ? '' : 'none';
    
    const warehouseBtn = document.getElementById('nav-warehouse-btn');
    if (warehouseBtn) warehouseBtn.style.display = (loggedIn && authUser?.role === 'warehouse') ? '' : 'none';

    const isSupport = authUser?.role === 'support' || authUser?.role === 'admin';
    const supportBtn = document.getElementById('nav-support-btn');
    if (supportBtn) supportBtn.style.display = (loggedIn && isSupport) ? '' : 'none';

    const supportDropdown = document.getElementById('user-dropdown-support');
    if (supportDropdown) supportDropdown.style.display = (loggedIn && isSupport) ? '' : 'none';
    
    if (loggedIn && authUser) {
        document.getElementById('nav-username').textContent = authUser.fullName || authUser.username;
    }
}

function getEffectiveChatSessionId() {
    if (isLoggedIn() && authUser?.id) {
        // Actor nội bộ (admin/warehouse/support) dùng session riêng: internal_{id}
        // Khách hàng dùng: user_{id}
        const role = authUser?.role || 'customer';
        if (role === 'admin' || role === 'warehouse' || role === 'support') {
            return 'internal_' + authUser.id;
        }
        return 'user_' + authUser.id;
    }
    let gid = localStorage.getItem('techstore_guest_chat_session');
    if (!gid) {
        gid = 'guest_' + Math.random().toString(36).substring(2, 9) + Date.now().toString(36).substring(4);
        localStorage.setItem('techstore_guest_chat_session', gid);
    }
    return gid;
}

function logout() {
    authToken = null; authUser = null; cartItems = [];
    localStorage.removeItem('token'); localStorage.removeItem('user');
    
    // Tách biệt phiên chat khi Đăng xuất: Đóng hộp chat, dừng toàn bộ polling
    isChatOpen = false;
    stopChatPolling();
    stopCskhDeskPolling();
    const chatBox = document.getElementById('floating-chat-box');
    if (chatBox) chatBox.style.display = 'none';
    localStorage.removeItem('techstore_chat_session');
    localStorage.removeItem('techstore_guest_chat_session');
    chatSessionId = getEffectiveChatSessionId();
    localStorage.setItem('techstore_chat_session', chatSessionId);
    
    renderChatMessages([]); // Dọn sạch tin nhắn của tài khoản vừa đăng xuất khỏi màn hình
    updateChatWidgetForRole(); // Khôi phục widget về mặc định cho guest
    
    updateNavAuth(); updateCartBadge();
    
    // Xóa trạng thái cskh-active trên html/body
    document.documentElement.classList.remove('cskh-active');
    document.body.classList.remove('cskh-active');

    // Nếu đang ở trang nội bộ (CSKH / Admin / Kho), chuyển hướng ngay lập tức về trang chủ
    const path = window.location.pathname.toLowerCase();
    const hash = window.location.hash.toLowerCase();
    if (path.includes('/support') || path.includes('/admin') || path.includes('/warehouse') ||
        hash.includes('support') || hash.includes('admin') || hash.includes('warehouse')) {
        window.location.href = '/';
        return;
    }

    showPage('home');
    toast('Đã đăng xuất thành công!', 'info');
}

/* API HELPERS */
async function api(method, path, body) {
    const headers = { 'Content-Type': 'application/json' };
    if (authToken) headers['Authorization'] = 'Bearer ' + authToken;
    const res = await fetch(API + path, {
        method, headers,
        body: body ? JSON.stringify(body) : undefined
    });
    const data = await res.json().catch(() => ({}));
    return { ok: res.ok, status: res.status, data };
}

/* TOAST */
function toast(msg, type = 'success') {
    const el = document.createElement('div');
    el.className = `toast toast-${type}`;
    el.textContent = msg;
    document.getElementById('toast-container').appendChild(el);
    setTimeout(() => el.remove(), 3500);
}

/* MODALS */
function openModal(id)  { document.getElementById(id).classList.add('open'); }
function closeModal(id) { document.getElementById(id).classList.remove('open'); }

function openAuthModal()  { openModal('auth-modal'); }
function openCart()       { openModal('cart-overlay'); document.getElementById('cart-drawer').classList.add('open'); loadCart(); }
function closeCart()      { closeModal('cart-overlay'); document.getElementById('cart-drawer').classList.remove('open'); }
async function openCheckout() {
    if (!isLoggedIn()) {
        toast('Vui lòng đăng nhập để tiến hành đặt hàng', 'info');
        openAuthModal();
        return;
    }
    if (!cartItems || cartItems.length === 0) {
        await syncCart();
    }
    if (!cartItems || cartItems.length === 0) {
        toast('Giỏ hàng của bạn đang trống! Vui lòng chọn sản phẩm vào giỏ trước khi thanh toán.', 'info');
        return;
    }
    closeCart();
    openModal('checkout-modal');
    await prefillCheckout();
}

document.addEventListener('keydown', e => {
    if (e.key === 'Escape') {
        ['auth-modal','checkout-modal','success-modal','profile-modal','vnpay-modal','create-product-modal','order-review-modal'].forEach(closeModal);
        closeCart();
    }
});

/* PAGE ROUTING */
function showPage(page, pushToHistory = true) {
    const routes = {
        'home': '/',
        'orders': '/orders',
        'support': '/support',
        'admin': '/admin',
        'warehouse': '/warehouse',
        'cart': '/cart'
    };

    const target = document.getElementById('page-' + page);
    if (!target) {
        if (routes[page]) {
            window.location.href = routes[page];
            return;
        }
    }

    ['home','product-detail','orders','admin','warehouse','support'].forEach(p => {
        const el = document.getElementById('page-' + p);
        if (el) el.style.display = 'none';
    });
    const catBar = document.getElementById('cat-bar');
    if (catBar) catBar.style.display = page === 'home' ? '' : 'none';
    const footer = document.getElementById('main-footer');
    if (footer) footer.style.display = (page === 'admin' || page === 'warehouse' || page === 'support') ? 'none' : '';

    if (target) { target.style.display = 'block'; target.classList.add('page'); }
    currentPage = page;

    if (pushToHistory) {
        const urlHash = page === 'home' ? '/' : (routes[page] || '#' + page);
        history.pushState({ page: page, productId: currentProductId }, "", urlHash);
    }

    if (page === 'orders') { 
        if (!isLoggedIn()) { 
            toast('Vui lòng đăng nhập để xem đơn hàng', 'info'); 
            openAuthModal(); 
            return; 
        } 
        loadOrders(); 
    }
    if (page === 'admin') { 
        if (!isAdmin()) { 
            toast('Bạn không có quyền truy cập trang Quản trị viên (Chỉ dành cho Admin)!', 'error'); 
            showPage('home'); 
            return; 
        } 
        loadAdminDashboard(); 
    }
    if (page === 'warehouse') { 
        if (authUser?.role !== 'warehouse' && authUser?.role !== 'admin') { 
            toast('Bạn không có quyền truy cập Quản lý kho (Chỉ dành cho Thủ kho / Admin)!', 'error'); 
            showPage('home'); 
            return; 
        } 
        loadWarehouseInventory(); 
    }
    if (page === 'support') {
        if (authUser?.role !== 'support' && authUser?.role !== 'admin') { 
            toast('Bạn không có quyền truy cập Bàn làm việc CSKH (Chỉ dành cho Nhân viên CSKH / Admin)!', 'error'); 
            showPage('home'); 
            return; 
        }
        document.documentElement.classList.add('cskh-active');
        document.documentElement.classList.add('cskh-active');
        document.body.classList.add('cskh-active');
        const floatChat = document.getElementById('floating-chat-container');
        if (floatChat) floatChat.style.display = 'none';
        loadSupportDesk(); 
    }
    if (page !== 'support') {
        document.documentElement.classList.remove('cskh-active');
        document.body.classList.remove('cskh-active');
        const floatChat = document.getElementById('floating-chat-container');
        if (floatChat) floatChat.style.display = '';
    }
    if (page === 'home')     loadProducts();

    window.scrollTo({ top: 0, behavior: 'smooth' });
}

function goBack() {
    if (history.state) {
        history.back();
    } else {
        showPage('home');
    }
}

window.addEventListener('popstate', async (e) => {
    if (e.state && e.state.page) {
        const statePage = e.state.page;
        if (statePage === 'product-detail' && e.state.productId) {
            await openProduct(e.state.productId, false);
        } else {
            showPage(statePage, false);
        }
    } else {
        showPage('home', false);
    }
});

/* AUTH */
function switchAuthTab(tab) {
    document.getElementById('tab-login').classList.toggle('active', tab === 'login');
    document.getElementById('tab-register').classList.toggle('active', tab === 'register');
    document.getElementById('panel-login').classList.toggle('active', tab === 'login');
    document.getElementById('panel-register').classList.toggle('active', tab === 'register');
}

async function doLogin() {
    const username = document.getElementById('login-username').value.trim();
    const password = document.getElementById('login-password').value;
    if (!username || !password) return toast('Vui lòng nhập đầy đủ thông tin', 'error');

    const btn = event.target; btn.textContent = 'Đang đăng nhập...'; btn.disabled = true;
    const { ok, data } = await api('POST', '/api/auth/login', { username, password });
    btn.textContent = 'Đăng nhập'; btn.disabled = false;

    if (!ok) return toast(data.message || 'Đăng nhập thất bại', 'error');

    authToken = data.token;
    authUser  = data.user;
    localStorage.setItem('token', authToken);
    localStorage.setItem('user', JSON.stringify(authUser));
    
    // Hợp nhất phiên khách (nếu có trước khi đăng nhập) vào tài khoản khách hàng
    const guestId = localStorage.getItem('techstore_guest_chat_session');
    if (guestId) {
        try {
            await api('POST', '/api/chat/merge-guest', { guestSessionId: guestId });
            localStorage.removeItem('techstore_guest_chat_session');
        } catch(e) {}
    }

    // Gắn phiên chat vĩnh viễn theo tài khoản khách hàng: user_{id}
    chatSessionId = getEffectiveChatSessionId();
    localStorage.setItem('techstore_chat_session', chatSessionId);
    await loadChatHistory();
    
    updateNavAuth();
    closeModal('auth-modal');
    toast(`Chào mừng, ${authUser.fullName || authUser.username}! 🎉`);
    await syncCart();
    updateChatWidgetForRole(); // Cập nhật widget theo role
    if (currentPage === 'admin' && isAdmin()) loadAdminDashboard();
    if (currentPage === 'support' && (authUser?.role === 'support' || authUser?.role === 'admin')) loadSupportDesk();
    if (currentPage === 'warehouse' && (authUser?.role === 'warehouse' || authUser?.role === 'admin')) loadWarehouseInventory();
    if (currentPage === 'orders') loadOrders();
}

async function doRegister() {
    const username = document.getElementById('reg-username').value.trim();
    const fullName = document.getElementById('reg-fullname').value.trim();
    const email    = document.getElementById('reg-email').value.trim();
    const phone    = document.getElementById('reg-phone').value.trim();
    const password = document.getElementById('reg-password').value;

    if (!username || !fullName || !email || !password)
        return toast('Vui lòng nhập đầy đủ thông tin', 'error');

    const btn = event.target; btn.textContent = 'Đang xử lý...'; btn.disabled = true;
    const { ok, data } = await api('POST', '/api/auth/register', { username, fullName, email, phone, password });
    btn.textContent = 'Tạo tài khoản'; btn.disabled = false;

    if (!ok) return toast(data.message || 'Đăng ký thất bại', 'error');
    toast('Đăng ký thành công! Hãy đăng nhập 🎉');
    switchAuthTab('login');
    document.getElementById('login-username').value = username;
}

/* PRODUCTS */
function filterCategory(el, slug) {
    activeCategory = slug;
    const homeEl = document.getElementById('page-home');
    if (!homeEl) {
        window.location.href = slug ? `/?cat=${encodeURIComponent(slug)}` : '/';
        return;
    }
    document.querySelectorAll('.cat-pill').forEach(p => p.classList.remove('active'));
    if (el) el.classList.add('active');
    else {
        const pill = document.querySelector(`.cat-pill[data-slug="${slug}"]`);
        if (pill) pill.classList.add('active');
    }
    if (currentPage !== 'home') {
        showPage('home');
    } else {
        loadProducts();
    }

    // Tự động cuộn mượt xuống phần danh sách sản phẩm
    setTimeout(() => {
        const target = document.getElementById('products-section');
        if (target) {
            target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }, 100);
}

function doSearch() {
    activeCategory = '';
    document.querySelectorAll('.cat-pill').forEach(p => p.classList.remove('active'));
    const allPill = document.querySelector('.cat-pill[data-slug=""]');
    if (allPill) allPill.classList.add('active');
    const homeEl = document.getElementById('page-home');
    if (!homeEl) {
        const search = document.getElementById('search-input')?.value.trim() || '';
        window.location.href = search ? `/?search=${encodeURIComponent(search)}` : '/';
        return;
    }
    if (currentPage !== 'home') showPage('home');
    else loadProducts();

    setTimeout(() => {
        const target = document.getElementById('products-section');
        if (target) {
            target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }, 100);
}

async function loadProducts() {
    const grid   = document.getElementById('products-grid');
    if (!grid) return;
    const sort   = document.getElementById('sort-select')?.value || '';
    const brand  = document.getElementById('brand-select')?.value || '';
    const search = document.getElementById('search-input')?.value.trim() || '';

    // Đồng bộ URL query params trên trang chủ không cần tải lại trang
    if (currentPage === 'home') {
        try {
            const url = new URL(window.location);
            if (activeCategory) url.searchParams.set('cat', activeCategory);
            else url.searchParams.delete('cat');

            if (brand) url.searchParams.set('brand', brand);
            else url.searchParams.delete('brand');

            if (search) url.searchParams.set('search', search);
            else url.searchParams.delete('search');

            if (sort) url.searchParams.set('sort', sort);
            else url.searchParams.delete('sort');

            const newSearch = url.searchParams.toString();
            const newUrl = url.pathname + (newSearch ? '?' + newSearch : '');
            if (window.location.search !== (newSearch ? '?' + newSearch : '')) {
                history.replaceState(null, '', newUrl);
            }
        } catch (e) {
            console.error('Lỗi cập nhật URL filter:', e);
        }
    }

    // Show skeletons
    grid.innerHTML = Array(8).fill(0).map(() => `
        <div class="skeleton-card">
            <div class="skeleton skeleton-img"></div>
            <div class="skeleton-body">
                <div class="skeleton skeleton-line" style="width:60%"></div>
                <div class="skeleton skeleton-line" style="width:90%"></div>
                <div class="skeleton skeleton-line" style="width:45%"></div>
            </div>
        </div>
    `).join('');

    let url = `/api/products?`;
    if (activeCategory) url += `category=${encodeURIComponent(activeCategory)}&`;
    if (brand)          url += `brand=${encodeURIComponent(brand)}&`;
    if (search)         url += `search=${encodeURIComponent(search)}&`;
    if (sort)           url += `sort=${encodeURIComponent(sort)}`;

    const { ok, data } = await api('GET', url);
    if (!ok) { grid.innerHTML = '<p style="color:var(--danger)">Lỗi tải sản phẩm</p>'; return; }

    document.getElementById('result-count').textContent = `${data.length} sản phẩm`;
    grid.innerHTML = data.length ? data.map(renderProductCard).join('') : `
        <div style="grid-column:1/-1;text-align:center;padding:60px;color:var(--text-3)">
            <div style="font-size:48px;margin-bottom:16px">🔍</div>
            <h3>Không tìm thấy sản phẩm</h3>
        </div>`;
}

function renderProductCard(p) {
    const price = p.salePrice > 0 ? p.salePrice : p.price;
    const save  = p.salePrice > 0 ? Math.round((1 - p.salePrice/p.price)*100) : 0;
    const stockClass = p.stockQuantity > 10 ? 'in' : p.stockQuantity > 0 ? 'low' : 'out';
    const stockText  = p.stockQuantity > 10 ? '✓ Còn hàng' : p.stockQuantity > 0 ? `⚠ Còn ${p.stockQuantity}` : '✗ Hết hàng';

    return `
    <div class="product-card" onclick="openProduct(${p.id})">
        <div class="product-img-wrap">
            ${p.badge ? `<div class="product-badge"><span class="badge badge-${p.badge}">${p.badge.toUpperCase()}</span></div>` : ''}
            <img src="${p.primaryImage || '/images/placeholder.svg'}" alt="${p.name}" loading="lazy" onerror="this.onerror=null;this.src='/images/placeholder.svg';">
        </div>
        <div class="product-body">
            <div class="product-brand">${p.brandName}</div>
            <div class="product-name">${p.name}</div>
            <div class="product-meta">
                <div class="stars">${renderStars(p.ratingAvg)}</div>
                <span class="product-rating">(${p.reviewCount})</span>
            </div>
            <div class="product-price">
                <span class="price-sale">${fmt(price)}</span>
                ${p.salePrice > 0 ? `<span class="price-orig">${fmt(p.price)}</span><span class="price-save">-${save}%</span>` : ''}
            </div>
            <div class="stock-chip ${stockClass}">${stockText}</div>
        </div>
    </div>`;
}

function renderStars(rating) {
    return Array.from({length:5}, (_,i) => `<span>${i < Math.round(rating) ? '★' : '☆'}</span>`).join('');
}

/* PRODUCT DETAIL */
function openProduct(id) {
    if (!id) return;
    window.location.href = '/product/' + id;
}

function switchGallery(el, src) {
    const main = document.getElementById('gallery-main-img') || document.getElementById('pdetail-primary-image');
    if (main) main.src = src;
    document.querySelectorAll('.gallery-thumb, .pdetail-thumb-item').forEach(t => t.classList.remove('active'));
    if (el) el.classList.add('active');
}

function renderReviews(reviews) {
    const el = document.getElementById('reviews-list');
    el.innerHTML = reviews.length
        ? reviews.map(r => `
            <div class="review-item">
                <div class="review-header">
                    <span class="review-user">${esc(r.username)}</span>
                    <span class="review-date">${new Date(r.createdAt).toLocaleDateString('vi-VN')}</span>
                </div>
                <div class="stars">${renderStars(r.rating)}</div>
                <p style="margin-top:8px;color:var(--text-2)">${esc(r.comment) || '(Không có bình luận)'}</p>
            </div>`).join('')
        : '<p style="color:var(--text-3);font-style:italic">Chưa có đánh giá nào. Hãy là người đầu tiên!</p>';
}

function changeDetailQty(d) {
    currentDetailQty = Math.max(1, currentDetailQty + d);
    document.getElementById('detail-qty').textContent = currentDetailQty;
}

async function addDetailToCart() {
    if (!isLoggedIn()) { openAuthModal(); return; }
    await doAddToCart(currentProductId, currentDetailQty);
}

async function buyNow() {
    await addDetailToCart();
    if (isLoggedIn()) openCart();
}

function setRating(v) {
    selectedRating = v;
    document.querySelectorAll('#star-picker span').forEach((s, i) => {
        s.classList.toggle('active', i < v);
    });
}

async function submitReview() {
    if (!isLoggedIn()) { openAuthModal(); return; }
    if (!selectedRating) return toast('Vui lòng chọn số sao', 'error');
    const comment = document.getElementById('review-comment').value.trim();

    const { ok, data } = await api('POST', '/api/reviews', {
        productId: currentProductId,
        rating: selectedRating,
        comment
    });

    if (!ok) return toast(data?.message || 'Gửi đánh giá thất bại', 'error');
    toast(data?.message || 'Cảm ơn đánh giá của bạn! ⭐');
    document.getElementById('review-comment').value = '';
    setRating(0);
    // Reload reviews
    const { data: fresh } = await api('GET', `/api/reviews/${currentProductId}`);
    renderReviews(fresh || []);
}

/* CART */
function fmt(n) {
    return new Intl.NumberFormat('vi-VN', { style:'currency', currency:'VND' }).format(n);
}

function esc(s) {
    if (s == null) return '';
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function updateCartBadge() {
    const total = cartItems.reduce((s, i) => s + i.quantity, 0);
    const badge = document.getElementById('cart-count');
    badge.style.display = total > 0 ? '' : 'none';
    badge.textContent   = total > 99 ? '99+' : total;
}

async function loadCart() {
    const el = document.getElementById('cart-items-list');
    const footer = document.getElementById('cart-footer');
    if (!isLoggedIn()) {
        if (el) el.innerHTML = `<div class="cart-empty-msg"><div class="icon">🔒</div><p>Vui lòng đăng nhập để xem giỏ hàng</p><br><button class="btn btn-primary" onclick="closeCart();openAuthModal()">Đăng nhập</button></div>`;
        if (footer) footer.style.display = 'none';
        return;
    }

    if (el) el.innerHTML = '<div class="spinner"></div>';
    await syncCart();
}

function renderCart() {
    const el = document.getElementById('cart-items-list');
    const footer = document.getElementById('cart-footer');
    const pageEl = document.getElementById('cart-page-items');
    const actionsEl = document.getElementById('cart-page-actions');

    if (!isLoggedIn()) {
        if (el) el.innerHTML = `<div class="cart-empty-msg"><div class="icon">🔒</div><p>Vui lòng đăng nhập để xem giỏ hàng</p><br><button class="btn btn-primary" onclick="closeCart();openAuthModal()">Đăng nhập</button></div>`;
        if (footer) footer.style.display = 'none';
        if (actionsEl) actionsEl.style.display = 'none';
        if (pageEl) {
            pageEl.innerHTML = `
                <div style="text-align:center;padding:60px 20px;color:var(--text-3)">
                    <div style="font-size:60px;margin-bottom:14px">🔒</div>
                    <h3 style="color:var(--text);margin-bottom:8px">Vui lòng đăng nhập</h3>
                    <p style="font-size:14px;color:var(--text-2);margin-bottom:20px">Đăng nhập tài khoản của bạn để xem giỏ hàng và tiến hành đặt hàng!</p>
                    <button type="button" class="btn btn-primary" onclick="openAuthModal()">Đăng nhập ngay</button>
                </div>`;
        }
        return;
    }

    if (!cartItems || !cartItems.length) {
        if (el) el.innerHTML = `<div class="cart-empty-msg"><div class="icon">🛒</div><p style="font-weight:600">Giỏ hàng trống</p><p style="font-size:13px;margin-top:8px">Hãy thêm sản phẩm vào giỏ!</p></div>`;
        if (footer) footer.style.display = 'none';
        if (actionsEl) actionsEl.style.display = 'none';
        if (pageEl) {
            pageEl.innerHTML = `
                <div style="text-align:center;padding:60px 20px;color:var(--text-3)">
                    <div style="font-size:60px;margin-bottom:14px">🛒</div>
                    <h3 style="color:var(--text);margin-bottom:8px">Giỏ hàng của bạn đang trống</h3>
                    <p style="font-size:14px;color:var(--text-2);margin-bottom:20px">Hãy khám phá thêm hàng trăm sản phẩm công nghệ hot tại TechStore!</p>
                    <a href="/" class="btn btn-primary">Khám phá sản phẩm ngay</a>
                </div>`;
        }
        return;
    }

    if (actionsEl) actionsEl.style.display = 'flex';

    if (el) {
        el.innerHTML = cartItems.map(item => `
            <div class="cart-item">
                <a href="/product/${item.productId}" style="display:block;flex-shrink:0" title="Bấm xem chi tiết">
                    <img class="cart-item-img" src="${item.productImage || '/images/placeholder.svg'}" onerror="this.onerror=null;this.src='/images/placeholder.svg';" alt="${item.productName}">
                </a>
                <div class="cart-item-info">
                    <a href="/product/${item.productId}" style="text-decoration:none;color:var(--text);font-weight:700;display:block;font-size:14px;line-height:1.3" onmouseover="this.style.color='var(--primary)'" onmouseout="this.style.color='var(--text)'" title="Xem chi tiết sản phẩm">
                        ${item.productName}
                    </a>
                    ${(item.variant || item.color) ? `
                        <div class="cart-item-attr" style="font-size:11.5px;color:var(--text-3);margin:3px 0 5px;display:flex;gap:6px;flex-wrap:wrap">
                            ${item.variant ? `<span style="background:var(--bg-2);padding:1px 6px;border-radius:4px;border:1px solid var(--border)">Bản: <b>${esc(item.variant)}</b></span>` : ''}
                            ${item.color ? `<span style="background:var(--bg-2);padding:1px 6px;border-radius:4px;border:1px solid var(--border)">Màu: <b>${esc(item.color)}</b></span>` : ''}
                        </div>
                    ` : ''}
                    <div class="cart-item-price">${fmt(item.salePrice > 0 ? item.salePrice : item.price)}</div>
                    <div class="cart-qty">
                        <button class="qty-btn" onclick="updateQty(${item.cartItemId},${item.quantity - 1})">−</button>
                        <span class="qty-val">${item.quantity}</span>
                        <button class="qty-btn" onclick="updateQty(${item.cartItemId},${item.quantity + 1})">+</button>
                    </div>
                </div>
                <button class="cart-remove" onclick="removeCartItem(${item.cartItemId})" title="Xóa khỏi giỏ hàng">✕</button>
            </div>`).join('');
    }

    const total = cartItems.reduce((s, i) => s + i.subtotal, 0);
    const totalPriceEl = document.getElementById('cart-total-price');
    if (totalPriceEl) totalPriceEl.textContent = fmt(total);
    if (footer) footer.style.display = '';

    // Cập nhật trang /cart nếu đang mở
    if (pageEl) {
        pageEl.innerHTML = `
            <div style="overflow-x:auto">
                <table style="width:100%;border-collapse:collapse;min-width:650px">
                    <thead>
                        <tr style="border-bottom:2px solid var(--border);text-align:left;color:var(--text-3);font-size:13.5px">
                            <th style="padding:12px">Sản phẩm</th>
                            <th style="padding:12px">Phân loại</th>
                            <th style="padding:12px">Đơn giá</th>
                            <th style="padding:12px;text-align:center">Số lượng</th>
                            <th style="padding:12px;text-align:right">Thành tiền</th>
                            <th style="padding:12px;text-align:center">Xóa</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${cartItems.map(item => `
                            <tr style="border-bottom:1px solid var(--border)">
                                <td style="padding:14px 12px">
                                    <div style="display:flex;align-items:center;gap:14px">
                                        <a href="/product/${item.productId}" style="flex-shrink:0" title="Xem chi tiết">
                                            <img src="${item.productImage || '/images/placeholder.svg'}" onerror="this.src='/images/placeholder.svg'" style="width:68px;height:68px;border-radius:10px;object-fit:cover;border:1px solid var(--border)">
                                        </a>
                                        <div>
                                            <a href="/product/${item.productId}" style="font-weight:700;color:var(--text);text-decoration:none;font-size:15px;display:block" onmouseover="this.style.color='var(--primary)'" onmouseout="this.style.color='var(--text)'">
                                                ${esc(item.productName)}
                                            </a>
                                            <div style="font-size:12px;color:var(--text-3);margin-top:2px">Mã SP: #${item.productId}</div>
                                        </div>
                                    </div>
                                </td>
                                <td style="padding:14px 12px;font-size:13px">
                                    ${(item.variant || item.color) ? `
                                        <div style="display:flex;flex-direction:column;gap:3px">
                                            ${item.variant ? `<span>📦 Bản: <b>${esc(item.variant)}</b></span>` : ''}
                                            ${item.color ? `<span>🎨 Màu: <b>${esc(item.color)}</b></span>` : ''}
                                        </div>
                                    ` : '<span style="color:var(--text-3)">Tiêu chuẩn</span>'}
                                </td>
                                <td style="padding:14px 12px;font-weight:600;font-size:14px">
                                    ${fmt(item.salePrice > 0 ? item.salePrice : item.price)}
                                </td>
                                <td style="padding:14px 12px;text-align:center">
                                    <div style="display:inline-flex;align-items:center;border:1.5px solid var(--border);border-radius:8px;background:var(--bg)">
                                        <button type="button" class="qty-btn" onclick="updateQty(${item.cartItemId},${item.quantity - 1})" style="width:32px;height:32px;border:none;background:none;cursor:pointer;font-weight:700;font-size:16px">−</button>
                                        <span style="min-width:32px;text-align:center;font-weight:700;font-size:14px">${item.quantity}</span>
                                        <button type="button" class="qty-btn" onclick="updateQty(${item.cartItemId},${item.quantity + 1})" style="width:32px;height:32px;border:none;background:none;cursor:pointer;font-weight:700;font-size:16px">+</button>
                                    </div>
                                </td>
                                <td style="padding:14px 12px;text-align:right;font-weight:800;color:var(--primary);font-size:15px">
                                    ${fmt(item.subtotal)}
                                </td>
                                <td style="padding:14px 12px;text-align:center">
                                    <button type="button" class="btn btn-ghost btn-sm" style="color:var(--danger);font-size:16px" onclick="removeCartItem(${item.cartItemId})" title="Xóa khỏi giỏ hàng">✕</button>
                                </td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
            <div style="display:flex;justify-content:flex-end;align-items:center;gap:16px;padding-top:18px;border-top:1px solid var(--border)">
                <div style="font-size:15px;color:var(--text-2)">
                    Tổng tiền hàng: <b style="font-size:22px;color:var(--primary);margin-left:6px">${fmt(total)}</b>
                </div>
            </div>`;
    }
}

async function quickAddToCart(id, name, price) {
    openProduct(id);
}

async function doAddToCart(productId, qty, variant = null, color = null) {
    if (!isLoggedIn()) { 
        toast('Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng', 'info');
        openAuthModal(); 
        return; 
    }
    const payload = { productId, quantity: qty };
    if (variant) payload.variant = variant;
    if (color) payload.color = color;

    const { ok, data } = await api('POST', '/api/cart', payload);
    if (!ok) { toast(data?.message || 'Thêm vào giỏ thất bại', 'error'); return; }

    await syncCart();
    toast('🛒 Đã thêm vào giỏ hàng thành công!');
}

async function syncCart() {
    if (!isLoggedIn()) {
        cartItems = [];
        updateCartBadge();
        renderCart();
        return;
    }
    const { ok, data } = await api('GET', '/api/cart');
    if (ok && Array.isArray(data)) {
        cartItems = data;
        updateCartBadge();
        renderCart();
    }
}

async function updateQty(cartItemId, qty) {
    if (qty < 1) { await removeCartItem(cartItemId); return; }
    const { ok, data } = await api('PUT', `/api/cart/${cartItemId}`, { quantity: qty });
    if (!ok) { toast(data?.message || 'Cập nhật thất bại', 'error'); return; }
    await syncCart();
}

async function removeCartItem(cartItemId) {
    const { ok } = await api('DELETE', `/api/cart/${cartItemId}`);
    if (!ok) { toast('Xóa thất bại', 'error'); return; }
    await syncCart();
    toast('Đã xóa sản phẩm khỏi giỏ hàng', 'info');
}

/* CHECKOUT */
let currentCheckoutStep = 1;

async function prefillCheckout() {
    appliedPromo = null;
    const promoInput = document.getElementById('co-promo');
    if (promoInput) promoInput.value = '';
    const promoMsg = document.getElementById('promo-msg');
    if (promoMsg) promoMsg.textContent = '';

    if (!vnProvinces || vnProvinces.length === 0) {
        await loadVnProvinces();
    }

    if (authUser) {
        const nameEl = document.getElementById('co-name');
        const emailEl = document.getElementById('co-email');
        const phoneEl = document.getElementById('co-phone');
        if (nameEl) nameEl.value  = authUser.fullName || '';
        if (emailEl) emailEl.value = authUser.email || '';
        if (phoneEl) phoneEl.value = authUser.phone || '';
        await applyProfileAddressToCheckout();
    }
    checkoutStep(1);
}

async function applyProfileAddressToCheckout() {
    if (!authUser || !authUser.address) return;
    const parts = authUser.address.split(',').map(s => s.trim());
    if (parts.length >= 4) {
        const street = parts.slice(0, parts.length - 3).join(', ');
        const ward = parts[parts.length - 3];
        const district = parts[parts.length - 2];
        const province = parts[parts.length - 1];

        const provSelect = document.getElementById('co-province');
        if (provSelect) {
            const opt = Array.from(provSelect.options).find(o => o.value === province || o.text.includes(province) || province.includes(o.text));
            if (opt) provSelect.value = opt.value;
            else provSelect.value = province;

            await onCheckoutProvinceChange();
            const distSelect = document.getElementById('co-district');
            if (distSelect) {
                const distOpt = Array.from(distSelect.options).find(o => o.value === district || o.text.includes(district) || district.includes(o.text));
                if (distOpt) distSelect.value = distOpt.value;
                else distSelect.value = district;

                await onCheckoutDistrictChange();
                const wardSelect = document.getElementById('co-ward');
                if (wardSelect) {
                    const wardOpt = Array.from(wardSelect.options).find(o => o.value === ward || o.text.includes(ward) || ward.includes(o.text));
                    if (wardOpt) wardSelect.value = wardOpt.value;
                    else wardSelect.value = ward;
                }
            }
        }
        const streetEl = document.getElementById('co-street');
        if (streetEl) streetEl.value = street;
    } else {
        const streetEl = document.getElementById('co-street');
        if (streetEl) streetEl.value = authUser.address;
    }
}

function getCheckoutFullAddress() {
    const province = document.getElementById('co-province')?.value || '';
    const district = document.getElementById('co-district')?.value || '';
    const ward     = document.getElementById('co-ward')?.value || '';
    const street   = document.getElementById('co-street')?.value.trim() || '';
    return `${street}, ${ward}, ${district}, ${province}`.trim().replace(/^,\s*/, '');
}

function goToCheckoutStep(targetStep) {
    if (targetStep === 2) {
        const name     = document.getElementById('co-name');
        const phone    = document.getElementById('co-phone');
        const email    = document.getElementById('co-email');
        const province = document.getElementById('co-province');
        const district = document.getElementById('co-district');
        const ward     = document.getElementById('co-ward');
        const street   = document.getElementById('co-street');

        // Reset borders
        [name, phone, email, province, district, ward, street].forEach(el => {
            if (el) el.style.borderColor = '';
        });

        let missing = [];
        if (!name?.value.trim()) { missing.push('Họ và tên'); if (name) name.style.borderColor = 'var(--danger)'; }
        if (!phone?.value.trim()) { missing.push('Số điện thoại'); if (phone) phone.style.borderColor = 'var(--danger)'; }
        if (!email?.value.trim()) { missing.push('Email'); if (email) email.style.borderColor = 'var(--danger)'; }
        if (!province?.value) { missing.push('Tỉnh / Thành phố'); if (province) province.style.borderColor = 'var(--danger)'; }
        if (!district?.value) { missing.push('Quận / Huyện'); if (district) district.style.borderColor = 'var(--danger)'; }
        if (!ward?.value) { missing.push('Phường / Xã'); if (ward) ward.style.borderColor = 'var(--danger)'; }
        if (!street?.value.trim()) { missing.push('Số nhà, tên đường'); if (street) street.style.borderColor = 'var(--danger)'; }

        if (missing.length > 0) {
            return toast('⚠️ Vui lòng điền và chọn đầy đủ: ' + missing.join(', '), 'error');
        }

        const phoneVal = phone.value.trim();
        const phoneRegex = /^(0|\+84)[35789][0-9]{8}$/;
        if (!phoneRegex.test(phoneVal)) {
            phone.style.borderColor = 'var(--danger)';
            return toast('Số điện thoại không hợp lệ (10 số, đầu số VN 03/05/07/08/09)', 'error');
        }
    }
    checkoutStep(targetStep);
}

function checkoutStep(step) {
    [1,2,3].forEach(s => {
        document.getElementById(`checkout-step-${s}`).style.display = s === step ? '' : 'none';
        const stepEl = document.getElementById(`step-${s}`);
        stepEl.className = `step-item ${s < step ? 'done' : s === step ? 'active' : ''}`;
    });

    if (step === 2) renderCheckoutSummary();
    if (step === 3) renderCheckoutReview();
    currentCheckoutStep = step;
}

function selectPayment(method) {
    selectedPayment = method;
    document.getElementById('pay-cod').classList.toggle('selected', method === 'cod');
    document.getElementById('pay-online').classList.toggle('selected', method === 'online');
}

function renderCheckoutSummary() {
    let total = cartItems.reduce((s, i) => s + i.subtotal, 0);
    let discountStr = '';
    if (appliedPromo) {
        let discount = appliedPromo.discountType === 'percent' ? total * (appliedPromo.discountValue / 100) : appliedPromo.discountValue;
        let finalTotal = Math.max(0, total - discount);
        discountStr = `
        <div style="display:flex;justify-content:space-between;margin-bottom:8px;color:var(--danger);font-weight:600">
            <span>Giảm giá (${appliedPromo.code})</span><span>-${fmt(discount)}</span>
        </div>`;
        total = finalTotal;
    }
    document.getElementById('checkout-summary').innerHTML = `
        <div style="display:flex;justify-content:space-between;margin-bottom:8px">
            <span>${cartItems.length} sản phẩm</span>
            <span style="font-weight:700">${fmt(cartItems.reduce((s, i) => s + i.subtotal, 0))}</span>
        </div>
        ${discountStr}
        <div style="display:flex;justify-content:space-between;margin-bottom:8px">
            <span>Phí giao hàng</span><span style="color:var(--success)">Miễn phí</span>
        </div>
        <div style="display:flex;justify-content:space-between;font-size:18px;font-weight:800;border-top:1px solid var(--border);padding-top:12px;margin-top:8px">
            <span>Tổng cộng</span><span style="color:var(--primary)">${fmt(total)}</span>
        </div>`;
}

function renderCheckoutReview() {
    const name  = document.getElementById('co-name').value.trim();
    const phone = document.getElementById('co-phone').value.trim();
    const address = getCheckoutFullAddress();
    const note = document.getElementById('co-note').value.trim();
    let total = cartItems.reduce((s, i) => s + i.subtotal, 0);
    
    // Apply promo if any
    let discountStr = '';
    if (appliedPromo) {
        let discount = 0;
        if (appliedPromo.discountType === 'percent') discount = total * (appliedPromo.discountValue / 100);
        else discount = appliedPromo.discountValue;
        total = total - discount;
        if (total < 0) total = 0;
        discountStr = `<div style="display:flex;justify-content:space-between;font-size:14px;color:var(--danger)">
            <span>Giảm giá (${appliedPromo.code})</span><span>- ${fmt(discount)}</span>
        </div>`;
    }

    const payLabel = selectedPayment === 'cod' ? '💵 Tiền mặt (COD)' : '💳 Thanh toán chuyển khoản VietQR';

    document.getElementById('checkout-review').innerHTML = `
        <h4 style="margin-bottom:12px">📦 Thông tin giao hàng</h4>
        <p style="font-size:14px;color:var(--text-2);margin-bottom:4px"><b>${esc(name)}</b> | ${esc(phone)}</p>
        <p style="font-size:14px;color:var(--text-2);margin-bottom:8px">${esc(address)}</p>
        ${note ? `<p style="font-size:13px;color:var(--primary);margin-bottom:14px;font-style:italic">📝 Ghi chú: ${esc(note)}</p>` : ''}
        <h4 style="margin-bottom:12px">🛒 Sản phẩm</h4>
        ${cartItems.map(i => `
            <div style="display:flex;justify-content:space-between;font-size:14px;margin-bottom:8px;border-bottom:1px dashed var(--border);padding-bottom:6px">
                <div>
                    <a href="/product/${i.productId}" target="_blank" style="text-decoration:none;color:var(--text);font-weight:600" onmouseover="this.style.color='var(--primary)'" onmouseout="this.style.color='var(--text)'">
                        ${esc(i.productName)}
                    </a>
                    ${(i.variant || i.color) ? `
                        <div style="font-size:12px;color:var(--text-3);margin-top:2px">
                            ${[i.variant ? `Bản: <b>${esc(i.variant)}</b>` : '', i.color ? `Màu: <b>${esc(i.color)}</b>` : ''].filter(Boolean).join(' • ')}
                        </div>
                    ` : ''}
                </div>
                <div style="text-align:right">
                    <span style="color:var(--text-3);font-size:13px">x${i.quantity}</span>
                    <div style="font-weight:700;color:var(--primary)">${fmt(i.subtotal)}</div>
                </div>
            </div>`).join('')}
        ${discountStr}
        <div style="border-top:1px solid var(--border);margin-top:12px;padding-top:12px;display:flex;justify-content:space-between;font-weight:800">
            <span>Tổng cộng</span><span style="color:var(--primary)">${fmt(total)}</span>
        </div>
        <div style="margin-top:12px;padding:10px 14px;background:var(--primary-light);border-radius:var(--radius-sm);font-size:14px;font-weight:600;color:var(--primary)">
            ${payLabel}
        </div>`;
}

async function confirmOrder() {
    if (!isLoggedIn()) {
        toast('Vui lòng đăng nhập để tiến hành đặt hàng', 'info');
        closeModal('checkout-modal');
        openAuthModal();
        return;
    }

    if (!cartItems || cartItems.length === 0) {
        return toast('Giỏ hàng của bạn đang trống! Vui lòng chọn sản phẩm vào giỏ trước.', 'error');
    }

    const name     = document.getElementById('co-name')?.value.trim() || '';
    const phone    = document.getElementById('co-phone')?.value.trim() || '';
    const email    = document.getElementById('co-email')?.value.trim() || '';
    const province = document.getElementById('co-province')?.value || '';
    const district = document.getElementById('co-district')?.value || '';
    const ward     = document.getElementById('co-ward')?.value || '';
    const street   = document.getElementById('co-street')?.value.trim() || '';
    const note     = document.getElementById('co-note')?.value.trim() || '';

    if (!name || !phone || !email || !province || !district || !ward || !street) {
        checkoutStep(1);
        return toast('Vui lòng điền và chọn đầy đủ thông tin giao hàng', 'error');
    }

    const fullAddress = `${street}, ${ward}, ${district}, ${province}`;
    let total = cartItems.reduce((s, i) => s + (i.subtotal || ((i.salePrice > 0 ? i.salePrice : i.price) * (i.quantity || 1))), 0);
    const cartForApi = cartItems.map(i => ({
        id: i.productId || i.id, 
        name: i.productName || i.name,
        price: (i.salePrice > 0 ? i.salePrice : i.price) || 0, 
        qty: i.quantity || i.qty || 1,
        variant: i.variant || null,
        color: i.color || null
    }));

    const btn = document.getElementById('confirm-order-btn');
    if (btn) { btn.textContent = 'Đang xử lý đặt hàng...'; btn.disabled = true; }

    const { ok, data } = await api('POST', '/api/orders/checkout', {
        customerInfo: { name, phone, email, city: province, district, address: fullAddress,
                        note: note,
                        paymentMethod: selectedPayment },
        cart: cartForApi,
        totalAmount: total,
        promotionCode: appliedPromo ? appliedPromo.code : null
    });

    if (btn) { btn.textContent = '✅ Xác nhận đặt hàng'; btn.disabled = false; }
    if (!ok) return toast(data?.message || 'Đặt hàng thất bại. Vui lòng kiểm tra lại!', 'error');

    closeModal('checkout-modal');
    cartItems = []; 
    updateCartBadge(); 
    renderCart();

    // If online payment, initiate VietQR payment flow
    if (selectedPayment === 'online' && data.orderCode) {
        document.getElementById('vnpay-order-code').textContent = data.orderCode;
        
        let finalAmt = total;
        if (appliedPromo) {
            let discount = appliedPromo.discountType === 'percent' ? total * (appliedPromo.discountValue / 100) : appliedPromo.discountValue;
            finalAmt = Math.max(0, total - discount);
        }
        document.getElementById('vnpay-amount').textContent = fmt(finalAmt);
        document.getElementById('vnpay-qr-img').src = `https://api.vietqr.io/image/970422-1220820056666-compact2.jpg?amount=${finalAmt}&addInfo=${encodeURIComponent(data.orderCode)}&accountName=HA%20PHUONG%20NAM`;
        
        openModal('vnpay-modal');
        window.currentVNPayOrderCode = data.orderCode;
    } else {
        document.getElementById('success-order-code').textContent = data.orderCode;
        openModal('success-modal');
    }
}

let appliedPromo = null;
async function applyPromo() {
    const code = document.getElementById('co-promo').value.trim().toUpperCase();
    const msgEl = document.getElementById('promo-msg');
    if (!code) { msgEl.textContent = 'Vui lòng nhập mã'; msgEl.style.color = 'var(--danger)'; return; }
    
    msgEl.textContent = 'Đang kiểm tra...'; msgEl.style.color = 'var(--text-2)';
    const { ok, data } = await api('GET', `/api/promotions/check?code=${code}`);
    
    if (!ok) {
        appliedPromo = null;
        msgEl.textContent = data.message || 'Mã không hợp lệ';
        msgEl.style.color = 'var(--danger)';
        renderCheckoutSummary();
    } else {
        const total = cartItems.reduce((s, i) => s + i.subtotal, 0);
        if (total < data.minOrderValue) {
            appliedPromo = null;
            msgEl.textContent = `Đơn hàng tối thiểu ${fmt(data.minOrderValue)} để áp dụng`;
            msgEl.style.color = 'var(--danger)';
            renderCheckoutSummary();
        } else {
            appliedPromo = { code, discountType: data.discountType, discountValue: data.discountValue };
            msgEl.textContent = `Áp dụng thành công! Đã giảm ${data.discountType === 'percent' ? data.discountValue + '%' : fmt(data.discountValue)}`;
            msgEl.style.color = 'var(--success)';
            renderCheckoutSummary();
        }
    }
}

async function confirmVNPay() {
    if (!window.currentVNPayOrderCode) return;
    const btn = document.getElementById('vnpay-confirm-btn');
    btn.textContent = 'Đang ghi nhận...';
    btn.disabled = true;
    
    const code = window.currentVNPayOrderCode;
    
    btn.textContent = '📨 Đã chuyển tiền (Chờ duyệt)';
    btn.disabled = false;
    closeModal('vnpay-modal');
    
    toast('Đã ghi nhận yêu cầu chuyển khoản! Vui lòng chờ Admin/Thủ kho đối soát sao kê để duyệt thanh toán.', 'info');
    document.getElementById('success-order-code').textContent = code;
    openModal('success-modal');
    if (authToken) loadOrders();
}

function cancelVNPay() {
    closeModal('vnpay-modal');
    toast('Đơn hàng đã được tạo. Bạn có thể thanh toán sau trong mục Đơn mua.', 'info');
    if (window.currentVNPayOrderCode) {
        document.getElementById('success-order-code').textContent = window.currentVNPayOrderCode;
        openModal('success-modal');
    }
}

function copyOrderCode() {
    const code = document.getElementById('vnpay-order-code').textContent;
    if (navigator.clipboard) {
        navigator.clipboard.writeText(code).then(() => toast('Đã sao chép nội dung chuyển khoản!'));
    } else {
        toast(`Mã CK: ${code}`);
    }
}

function payOrderAgain(orderCode, amount) {
    window.currentVNPayOrderCode = orderCode;
    document.getElementById('vnpay-order-code').textContent = orderCode;
    document.getElementById('vnpay-amount').textContent = fmt(amount);
    document.getElementById('vnpay-qr-img').src = `https://api.vietqr.io/image/970422-1220820056666-compact2.jpg?amount=${amount}&addInfo=${encodeURIComponent(orderCode)}&accountName=HA%20PHUONG%20NAM`;
    openModal('vnpay-modal');
}

async function cancelOrder(id) {
    if (!confirm('Bạn có chắc chắn muốn hủy đơn hàng này? Số lượng sản phẩm sẽ được tự động hoàn lại vào kho.')) return;
    const { ok, data } = await api('POST', `/api/orders/${id}/cancel`);
    if (ok) {
        toast('Đã hủy đơn hàng và hoàn trả tồn kho thành công! ✓');
        loadOrders();
    } else {
        toast(data?.message || 'Không thể hủy đơn hàng', 'error');
    }
}

/* USER PROFILE & ADDRESS FUNCTIONS */
let vnProvinces = [];

async function loadVnProvinces() {
    try {
        const { ok, data } = await api('GET', '/api/address/provinces');
        if (ok && Array.isArray(data)) {
            vnProvinces = data;
            populateProvinceSelect('co-province', vnProvinces);
            populateProvinceSelect('prof-province', vnProvinces);
        }
    } catch (e) {
        console.error('Error loading VN provinces:', e);
    }
}

function populateProvinceSelect(elementId, provinces) {
    const el = document.getElementById(elementId);
    if (!el) return;
    const currentVal = el.value;
    el.innerHTML = '<option value="">-- Chọn Tỉnh / Thành phố --</option>' +
        provinces.map(p => `<option value="${p}">${p}</option>`).join('');
    if (currentVal) el.value = currentVal;
}

async function onCheckoutProvinceChange() {
    const province = document.getElementById('co-province').value;
    const distEl = document.getElementById('co-district');
    const wardEl = document.getElementById('co-ward');
    distEl.innerHTML = '<option value="">-- Đang tải Quận/Huyện... --</option>';
    wardEl.innerHTML = '<option value="">-- Chọn Phường/Xã --</option>';
    
    if (!province) {
        distEl.innerHTML = '<option value="">-- Chọn Quận/Huyện --</option>';
        return;
    }
    const { ok, data } = await api('GET', `/api/address/districts?province=${encodeURIComponent(province)}`);
    if (ok && Array.isArray(data)) {
        distEl.innerHTML = '<option value="">-- Chọn Quận/Huyện --</option>' +
            data.map(d => `<option value="${d}">${d}</option>`).join('');
    }
}

async function onCheckoutDistrictChange() {
    const district = document.getElementById('co-district').value;
    const wardEl = document.getElementById('co-ward');
    wardEl.innerHTML = '<option value="">-- Đang tải Phường/Xã... --</option>';
    
    if (!district) {
        wardEl.innerHTML = '<option value="">-- Chọn Phường/Xã --</option>';
        return;
    }
    const { ok, data } = await api('GET', `/api/address/wards?district=${encodeURIComponent(district)}`);
    if (ok && Array.isArray(data)) {
        wardEl.innerHTML = '<option value="">-- Chọn Phường/Xã --</option>' +
            data.map(w => `<option value="${w}">${w}</option>`).join('');
    }
}

async function onProfileProvinceChange() {
    const province = document.getElementById('prof-province').value;
    const distEl = document.getElementById('prof-district');
    const wardEl = document.getElementById('prof-ward');
    distEl.innerHTML = '<option value="">-- Đang tải Quận/Huyện... --</option>';
    wardEl.innerHTML = '<option value="">-- Chọn Phường/Xã --</option>';
    
    if (!province) {
        distEl.innerHTML = '<option value="">-- Chọn Quận/Huyện --</option>';
        return;
    }
    const { ok, data } = await api('GET', `/api/address/districts?province=${encodeURIComponent(province)}`);
    if (ok && Array.isArray(data)) {
        distEl.innerHTML = '<option value="">-- Chọn Quận/Huyện --</option>' +
            data.map(d => `<option value="${d}">${d}</option>`).join('');
    }
}

async function onProfileDistrictChange() {
    const district = document.getElementById('prof-district').value;
    const wardEl = document.getElementById('prof-ward');
    wardEl.innerHTML = '<option value="">-- Đang tải Phường/Xã... --</option>';
    
    if (!district) {
        wardEl.innerHTML = '<option value="">-- Chọn Phường/Xã --</option>';
        return;
    }
    const { ok, data } = await api('GET', `/api/address/wards?district=${encodeURIComponent(district)}`);
    if (ok && Array.isArray(data)) {
        wardEl.innerHTML = '<option value="">-- Chọn Phường/Xã --</option>' +
            data.map(w => `<option value="${w}">${w}</option>`).join('');
    }
}

function openProfileModal() {
    if (!isLoggedIn()) { openAuthModal(); return; }
    document.getElementById('prof-username').value = authUser.username || '';
    document.getElementById('prof-fullname').value = authUser.fullName || '';
    document.getElementById('prof-email').value = authUser.email || '';
    document.getElementById('prof-phone').value = authUser.phone || '';
    
    if (authUser.address) {
        const parts = authUser.address.split(',').map(s => s.trim());
        if (parts.length >= 4) {
            const street = parts.slice(0, parts.length - 3).join(', ');
            const ward = parts[parts.length - 3];
            const district = parts[parts.length - 2];
            const province = parts[parts.length - 1];

            const provSelect = document.getElementById('prof-province');
            if (provSelect) {
                provSelect.value = province;
                onProfileProvinceChange().then(() => {
                    const distSelect = document.getElementById('prof-district');
                    if (distSelect) {
                        distSelect.value = district;
                        onProfileDistrictChange().then(() => {
                            const wardSelect = document.getElementById('prof-ward');
                            if (wardSelect) wardSelect.value = ward;
                        });
                    }
                });
            }
            document.getElementById('prof-street').value = street;
        } else {
            document.getElementById('prof-street').value = authUser.address;
        }
    } else {
        document.getElementById('prof-street').value = '';
    }
    
    switchProfileTab('info');
    openModal('profile-modal');
}

function switchProfileTab(tab) {
    document.getElementById('profile-tab-info').style.display = tab === 'info' ? '' : 'none';
    document.getElementById('profile-tab-security').style.display = tab === 'security' ? '' : 'none';
    
    const btnInfo = document.getElementById('btn-tab-info');
    const btnSec = document.getElementById('btn-tab-security');
    if (tab === 'info') {
        btnInfo.className = 'btn btn-primary btn-sm profile-tab-btn';
        btnSec.className = 'btn btn-ghost btn-sm profile-tab-btn';
    } else {
        btnInfo.className = 'btn btn-ghost btn-sm profile-tab-btn';
        btnSec.className = 'btn btn-primary btn-sm profile-tab-btn';
    }
}

async function submitUpdateProfile(e) {
    e.preventDefault();
    const btn = document.getElementById('prof-submit-btn');
    btn.textContent = 'Đang lưu...'; btn.disabled = true;

    const fullName = document.getElementById('prof-fullname').value.trim();
    const email = document.getElementById('prof-email').value.trim();
    const phone = document.getElementById('prof-phone').value.trim();
    const province = document.getElementById('prof-province').value;
    const district = document.getElementById('prof-district').value;
    const ward = document.getElementById('prof-ward').value;
    const street = document.getElementById('prof-street').value.trim();

    let fullAddress = '';
    if (street && ward && district && province) {
        fullAddress = `${street}, ${ward}, ${district}, ${province}`;
    } else if (street) {
        fullAddress = street;
    }

    const { ok, data } = await api('PUT', '/api/auth/profile', {
        fullName, email, phone, address: fullAddress
    });

    btn.textContent = '💾 Lưu thay đổi'; btn.disabled = false;
    if (ok && data.user) {
        authUser = data.user;
        localStorage.setItem('user', JSON.stringify(authUser));
        updateNavAuth();
        toast('Cập nhật thông tin cá nhân thành công! 🎉');
        closeModal('profile-modal');
    } else {
        toast(data.message || 'Lỗi khi cập nhật thông tin', 'error');
    }
}

async function submitChangePassword(e) {
    e.preventDefault();
    const currentPassword = document.getElementById('pwd-current').value;
    const newPassword = document.getElementById('pwd-new').value;
    const confirmPassword = document.getElementById('pwd-confirm').value;

    if (newPassword !== confirmPassword) {
        return toast('Xác nhận mật khẩu mới không khớp!', 'error');
    }

    const btn = document.getElementById('pwd-submit-btn');
    btn.textContent = 'Đang cập nhật...'; btn.disabled = true;

    const { ok, data } = await api('PUT', '/api/auth/change-password', {
        currentPassword, newPassword
    });

    btn.textContent = '🔑 Cập nhật mật khẩu'; btn.disabled = false;
    if (ok) {
        toast('Đổi mật khẩu thành công! ✓');
        document.getElementById('pwd-current').value = '';
        document.getElementById('pwd-new').value = '';
        document.getElementById('pwd-confirm').value = '';
        closeModal('profile-modal');
    } else {
        toast(data.message || 'Lỗi khi đổi mật khẩu', 'error');
    }
}

function toggleUserDropdown(e) {
    e.stopPropagation();
    const menu = document.getElementById('user-dropdown-menu');
    menu.style.display = menu.style.display === 'none' ? 'block' : 'none';
}

function closeUserDropdown() {
    const menu = document.getElementById('user-dropdown-menu');
    if (menu) menu.style.display = 'none';
}

document.addEventListener('click', () => {
    closeUserDropdown();
});

/* ORDERS */
let currentOrderStatusFilter = 'all';

async function loadOrders() {
    const el = document.getElementById('orders-list');
    if (!el) return;

    if (!isLoggedIn()) {
        el.innerHTML = `
            <div style="text-align:center;padding:60px 20px;color:var(--text-3);background:var(--bg-card);border-radius:16px;border:1px dashed var(--border)">
                <div style="font-size:50px;margin-bottom:12px">🔒</div>
                <h3 style="color:var(--text);margin-bottom:8px">Vui lòng đăng nhập</h3>
                <p style="font-size:13.5px;color:var(--text-2);margin-bottom:16px">Đăng nhập tài khoản của bạn để xem và theo dõi danh sách đơn hàng đã mua!</p>
                <button type="button" class="btn btn-primary" onclick="openAuthModal()">Đăng nhập ngay</button>
            </div>`;
        return;
    }

    el.innerHTML = '<div class="spinner"></div>';
    
    const [ordersRes, reviewsRes] = await Promise.all([
        api('GET', '/api/orders'),
        api('GET', '/api/reviews/my-reviews')
    ]);

    if (!ordersRes.ok) { el.innerHTML = '<p style="color:var(--danger)">Lỗi tải đơn hàng</p>'; return; }
    const data = ordersRes.data || [];
    const myReviews = reviewsRes.ok && Array.isArray(reviewsRes.data) ? reviewsRes.data : [];
    window._ordersData = data;
    window._myReviewsData = myReviews;

    // Cập nhật số lượng đơn cho từng tab trạng thái
    const counts = {
        all: data.length,
        pending: data.filter(o => o.status === 'pending').length,
        processing: data.filter(o => o.status === 'processing').length,
        shipped: data.filter(o => o.status === 'shipped').length,
        delivered: data.filter(o => o.status === 'delivered').length,
        cancelled: data.filter(o => o.status === 'cancelled').length
    };

    for (const [st, cnt] of Object.entries(counts)) {
        const badge = document.getElementById(`count-order-${st}`);
        if (badge) badge.textContent = cnt;
    }

    renderOrderList();
}

function filterOrderStatus(status, btn) {
    currentOrderStatusFilter = status;
    document.querySelectorAll('#order-status-tabs .order-tab').forEach(b => b.classList.remove('active'));
    if (btn) btn.classList.add('active');
    renderOrderList();
}

function renderOrderList() {
    const el = document.getElementById('orders-list');
    const allData = window._ordersData || [];
    const myReviews = window._myReviewsData || [];

    if (!allData.length) {
        el.innerHTML = `<div style="text-align:center;padding:60px;color:var(--text-3)">
            <div style="font-size:60px;margin-bottom:16px">📦</div>
            <h3>Chưa có đơn hàng nào</h3>
            <button class="btn btn-primary" style="margin-top:20px" onclick="showPage('home')">Mua sắm ngay</button>
        </div>`;
        return;
    }

    const filtered = currentOrderStatusFilter === 'all' 
        ? allData 
        : allData.filter(o => o.status === currentOrderStatusFilter);

    if (!filtered.length) {
        const labels = {
            pending: 'Chờ xác nhận',
            processing: 'Đang xử lý',
            shipped: 'Đang giao hàng',
            delivered: 'Hoàn thành',
            cancelled: 'Đã hủy'
        };
        const curLabel = labels[currentOrderStatusFilter] || currentOrderStatusFilter;
        el.innerHTML = `
            <div style="text-align:center;padding:60px 20px;color:var(--text-3);background:var(--bg-card);border-radius:16px;border:1px dashed var(--border);margin-top:10px">
                <div style="font-size:50px;margin-bottom:12px">🔍</div>
                <h3 style="color:var(--text);margin-bottom:6px">Không có đơn hàng nào</h3>
                <p style="font-size:13.5px">Bạn hiện không có đơn nào ở trạng thái "<strong>${curLabel}</strong>".</p>
                <button class="btn btn-outline btn-sm" style="margin-top:16px" onclick="filterOrderStatus('all', document.querySelector('[data-status=all]'))">
                    Xem tất cả đơn hàng
                </button>
            </div>`;
        return;
    }

    const statusSteps = ['pending','processing','shipped','delivered'];
    el.innerHTML = filtered.map(o => {
        const curStep = statusSteps.indexOf(o.status);
        const timeline = statusSteps.map((s, i) => {
            const cls = i < curStep ? 'done' : i === curStep ? 'active' : '';
            const icons = ['📋','⚙️','🚚','✅'];
            const labels = ['Đặt hàng','Xử lý','Đang giao','Hoàn thành'];
            return `<div class="timeline-step ${cls}">
                <div class="timeline-dot">${icons[i]}</div>
                <div class="timeline-lbl">${labels[i]}</div>
            </div>`;
        }).join('');

        const isCancelled = o.status === 'cancelled';
        const isPaid = o.paymentStatus === 'paid';
        const canCancel = o.status === 'pending';
        const canPay = !isPaid && !isCancelled;

        return `<div class="order-card">
            <div class="order-head">
                <div>
                    <div class="order-code">#${esc(o.orderCode)}</div>
                    <div class="order-date">${new Date(o.orderedAt).toLocaleDateString('vi-VN')}</div>
                </div>
                <div style="display:flex;gap:10px;align-items:center">
                    <span class="status-badge status-${o.status}">${statusLabel(o.status)}</span>
                    <span style="font-size:11px;padding:3px 10px;border-radius:20px;background:${isPaid?'#f0fdf4':'#fef2f2'};color:${isPaid?'var(--success)':'var(--danger)'}">
                        ${isPaid ? '✓ Đã thanh toán' : isCancelled ? '– Đã hủy' : '⏳ Chưa thanh toán'}
                    </span>
                </div>
            </div>
            ${isCancelled ? '<div style="padding:10px 14px;background:#fef2f2;color:var(--danger);border-radius:8px;font-size:13px;margin-bottom:14px">🚫 Đơn hàng này đã được hủy và tồn kho đã được hoàn lại.</div>' : `<div class="order-timeline">${timeline}</div>`}
            <div class="order-items-list">
                ${o.items.map(i => {
                    const existingRev = myReviews.find(r => r.productId === i.productId && (!r.orderId || r.orderId === o.id));
                    return `
                    <div class="order-item-row" style="display:flex;align-items:center;gap:14px;padding:12px 0;border-bottom:1px solid var(--border)">
                        <a href="/product/${i.productId}" target="_blank" style="display:block;flex-shrink:0" title="Bấm xem chi tiết sản phẩm">
                            <img class="order-item-img" src="${i.productImage || '/images/placeholder.svg'}" onerror="this.src='/images/placeholder.svg'" alt="" style="width:64px;height:64px;object-fit:cover;border-radius:10px;border:1px solid var(--border);transition:transform .2s" onmouseover="this.style.transform='scale(1.05)'" onmouseout="this.style.transform='scale(1)'">
                        </a>
                        <div class="order-item-info" style="flex:1;min-width:0">
                            <a href="/product/${i.productId}" target="_blank" class="order-item-name" style="font-weight:700;font-size:14.5px;color:var(--text);text-decoration:none;display:block" onmouseover="this.style.color='var(--primary)'" onmouseout="this.style.color='var(--text)'" title="Xem chi tiết sản phẩm">
                                ${esc(i.productName)}
                            </a>
                            <div class="order-item-meta" style="color:var(--text-2);font-size:12.5px;margin-top:4px;display:flex;gap:8px;flex-wrap:wrap;align-items:center">
                                ${i.variant ? `<span style="background:var(--bg-2);padding:2px 8px;border-radius:6px;border:1px solid var(--border)">📦 Bản: <b>${esc(i.variant)}</b></span>` : ''}
                                ${i.color ? `<span style="background:var(--bg-2);padding:2px 8px;border-radius:6px;border:1px solid var(--border)">🎨 Màu: <b>${esc(i.color)}</b></span>` : ''}
                                <span style="color:var(--text-3)">Số lượng: <b>x${i.quantity}</b> × ${fmt(i.unitPrice)}</span>
                            </div>
                        </div>
                        <div style="display:flex;flex-direction:column;align-items:flex-end;gap:6px">
                            <div class="order-item-price" style="font-weight:800;font-size:15px;color:var(--primary)">${fmt(i.subtotal)}</div>
                            ${o.status === 'delivered' ? (existingRev ? `
                                <button type="button" class="btn btn-ghost btn-sm" style="color:var(--accent);font-size:11.5px;padding:3px 8px;border-radius:6px;border:1px solid rgba(245,158,11,.3);background:#fffbeb" 
                                        onclick="openOrderReviewModal(${o.id}, ${i.productId})">
                                    ⭐ ${existingRev.rating}★ Đã đánh giá (Sửa)
                                </button>
                            ` : `
                                <button type="button" class="btn btn-outline btn-sm" style="color:var(--primary);border-color:var(--primary);font-size:11.5px;padding:3px 10px;font-weight:600;border-radius:6px" 
                                        onclick="openOrderReviewModal(${o.id}, ${i.productId})">
                                    ⭐ Đánh giá sản phẩm
                                </button>
                            `) : (!isCancelled ? `
                                <span style="font-size:11.5px;color:var(--text-3);background:var(--bg-2);padding:3px 8px;border-radius:6px;border:1px dashed var(--border)" title="Bạn chỉ có thể đánh giá khi đơn hàng đã hoàn thành (giao thành công)">
                                    🕒 Đánh giá khi nhận hàng
                                </span>
                            ` : '')}
                        </div>
                    </div>`;
                }).join('')}
            </div>
            <div style="display:flex;justify-content:space-between;align-items:center;margin-top:16px;padding-top:16px;border-top:1px solid var(--border);flex-wrap:wrap;gap:10px">
                <span style="color:var(--text-2);font-size:13px">📍 ${esc(o.shippingAddress)}</span>
                <div style="display:flex;align-items:center;gap:12px">
                    <span style="font-size:17px;font-weight:800;color:var(--primary)">${fmt(o.totalAmount)}</span>
                    ${canPay ? `<button class="btn btn-primary btn-sm" onclick="payOrderAgain('${o.orderCode}', ${o.totalAmount})">💳 Thanh toán ngay</button>` : ''}
                    ${canCancel ? `<button class="btn btn-outline btn-sm" style="color:var(--danger);border-color:var(--danger)" onclick="cancelOrder(${o.id})">❌ Hủy đơn</button>` : ''}
                </div>
            </div>
        </div>`;
    }).join('');
}

let currentOrderRating = 5;
function setOrderRating(val) {
    currentOrderRating = val;
    document.querySelectorAll('#order-star-picker span').forEach((s, idx) => {
        s.classList.toggle('active', idx < val);
    });
    const texts = [
        "",
        "Rất tệ (1 sao)",
        "Không hài lòng (2 sao)",
        "Bình thường (3 sao)",
        "Hài lòng (4 sao)",
        "Tuyệt vời (5 sao)"
    ];
    document.getElementById('order-rating-text').textContent = texts[val] || '';
}

function openOrderReviewModal(orderId, productId) {
    const order = (window._ordersData || []).find(o => o.id === orderId);
    if (order && order.status !== 'delivered') {
        toast('Bạn chỉ có thể đánh giá sản phẩm sau khi đơn hàng đã hoàn thành (giao thành công)!', 'error');
        return;
    }
    const item = order?.items?.find(i => i.productId === productId);
    const rev = (window._myReviewsData || []).find(r => r.productId === productId && (!r.orderId || r.orderId === orderId));

    document.getElementById('rev-product-id').value = productId;
    document.getElementById('rev-order-id').value = orderId || '';
    document.getElementById('rev-product-name').textContent = item?.productName || 'Sản phẩm';
    document.getElementById('rev-order-code').textContent = order?.orderCode ? `Đơn hàng #${order.orderCode}` : '';
    document.getElementById('rev-product-img').src = item?.productImage || '/images/placeholder.svg';
    document.getElementById('rev-comment').value = rev?.comment || '';
    setOrderRating(rev?.rating || 5);
    openModal('order-review-modal');
}

async function submitOrderReview(e) {
    e.preventDefault();
    const btn = document.getElementById('rev-submit-btn');
    btn.textContent = 'Đang gửi...'; btn.disabled = true;

    const productId = parseInt(document.getElementById('rev-product-id').value);
    const orderId = parseInt(document.getElementById('rev-order-id').value) || null;
    const comment = document.getElementById('rev-comment').value.trim();

    const { ok, data } = await api('POST', '/api/reviews', {
        productId,
        orderId,
        rating: currentOrderRating,
        comment
    });

    btn.textContent = '⭐ Gửi Đánh Giá'; btn.disabled = false;

    if (ok) {
        toast('Cảm ơn bạn đã gửi đánh giá sản phẩm! ⭐⭐⭐⭐⭐');
        closeModal('order-review-modal');
        loadOrders();
        if (currentPage === 'home') loadProducts();
        if (currentPage === 'product-detail' && currentProductId === productId) {
            openProduct(productId);
        }
    } else {
        toast(data?.message || 'Lỗi khi gửi đánh giá', 'error');
    }
}

function statusLabel(s) {
    return { pending:'Chờ xử lý', processing:'Đang xử lý', shipped:'Đang giao', delivered:'Đã giao', cancelled:'Đã hủy' }[s] || s;
}

/* ADMIN */
function showAdminTab(tab, el) {
    ['dashboard','products','categories','orders','users'].forEach(t => {
        const pane = document.getElementById(`admin-tab-${t}`);
        if(pane) pane.style.display = t === tab ? '' : 'none';
    });
    document.querySelectorAll('.admin-nav-item').forEach(n => n.classList.remove('active'));
    if (el) el.classList.add('active');

    if (tab === 'dashboard')  loadAdminDashboard();
    if (tab === 'products')   loadAdminProducts();
    if (tab === 'categories') loadAdminCategories();
    if (tab === 'orders')     loadAdminOrders();
    if (tab === 'users')      loadAdminUsers();
}

async function loadAdminDashboard() {
    const { ok, data } = await api('GET', '/api/admin/dashboard');
    if (!ok || !data) return;

    const statsEl = document.getElementById('admin-stats');
    if (statsEl) {
        statsEl.innerHTML = `
            <div class="stat-card">
                <div class="stat-icon" style="background:#eef2ff">📦</div>
                <div class="stat-val">${data.totalOrders ?? 0}</div>
                <div class="stat-lbl">Đơn hàng hợp lệ</div>
            </div>
            <div class="stat-card">
                <div class="stat-icon" style="background:#f0fdf4">💰</div>
                <div class="stat-val">${fmt(data.totalRevenue ?? 0)}</div>
                <div class="stat-lbl">Doanh thu đã thanh toán</div>
            </div>
            <div class="stat-card">
                <div class="stat-icon" style="background:#fffbeb">👥</div>
                <div class="stat-val">${data.totalUsers ?? 0}</div>
                <div class="stat-lbl">Khách hàng</div>
            </div>
            <div class="stat-card">
                <div class="stat-icon" style="background:#fef2f2">⏳</div>
                <div class="stat-val">${data.pendingOrders ?? 0}</div>
                <div class="stat-lbl">Chờ xử lý</div>
            </div>`;
    }

    const titleEl = document.getElementById('admin-chart-title');
    if (titleEl) {
        const rev7 = data.revenue7Days != null ? data.revenue7Days : (data.revenueByDay || []).reduce((s, d) => s + (d.revenue || 0), 0);
        titleEl.innerHTML = `📈 Doanh thu 7 ngày qua <span style="font-size:14px;font-weight:700;color:var(--primary);margin-left:8px">(Tổng 7 ngày: ${fmt(rev7)})</span>`;
    }

    // Revenue chart
    const revList = data.revenueByDay || [];
    const revValues = revList.map(d => d.revenue);
    const maxRev = Math.max(...(revValues.length ? revValues : [1]), 1);
    const chartEl = document.getElementById('revenue-chart');
    if (chartEl) {
        chartEl.innerHTML = revList.length
            ? revList.map(d => `
                <div style="flex:1;display:flex;flex-direction:column;align-items:center;gap:6px">
                    <span style="font-size:11px;font-weight:700;color:var(--primary)">${fmt(d.revenue)}</span>
                    <div style="width:100%;background:var(--primary);border-radius:6px 6px 0 0;height:${Math.max(8,(d.revenue/maxRev)*120)}px;transition:.3s" title="${d.date}: ${fmt(d.revenue)} (${d.orderCount || 0} đơn)"></div>
                    <span style="font-size:11px;color:var(--text-3)">${d.date}</span>
                </div>`).join('')
            : '<p style="color:var(--text-3);padding:20px 0;width:100%;text-align:center">Chưa có dữ liệu doanh thu</p>';
    }
}


async function loadAdminProducts(isManualRefresh = false, btnEl = null) {
    if (isManualRefresh) {
        const sEl = document.getElementById('admin-product-search');
        const cEl = document.getElementById('admin-product-category');
        const bEl = document.getElementById('admin-product-brand');
        const stEl = document.getElementById('admin-product-status');
        if (sEl) sEl.value = '';
        if (cEl) cEl.value = '';
        if (bEl) bEl.value = '';
        if (stEl) stEl.value = '';
        if (btnEl) {
            btnEl.disabled = true;
            btnEl.innerHTML = '⏳ Đang tải...';
        }
    }

    const [, prodRes] = await Promise.all([
        (appCategories.length && appBrands.length && !isManualRefresh) ? Promise.resolve() : loadBrandsAndCategories(),
        api('GET', '/api/products')
    ]);

    if (btnEl) {
        btnEl.disabled = false;
        btnEl.innerHTML = '🔄 Làm mới';
    }

    const { ok, data } = prodRes;
    if (!ok || !Array.isArray(data)) return;
    window.adminProductsCache = data;

    const catFilter = document.getElementById('admin-product-category');
    if (catFilter && appCategories.length) {
        const curCat = isManualRefresh ? '' : catFilter.value;
        catFilter.innerHTML = `<option value="">Tất cả danh mục</option>` +
            appCategories.map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
        if (curCat) catFilter.value = curCat;
    }
    const brandFilter = document.getElementById('admin-product-brand');
    if (brandFilter && appBrands.length) {
        const curBrand = isManualRefresh ? '' : brandFilter.value;
        brandFilter.innerHTML = `<option value="">Tất cả thương hiệu</option>` +
            appBrands.map(b => `<option value="${b.id}">${esc(b.name)}</option>`).join('');
        if (curBrand) brandFilter.value = curBrand;
    }

    filterAdminProducts();
    if (isManualRefresh) {
        toast(`Đã làm mới danh sách (${data.length} sản phẩm) & đặt lại bộ lọc! 🔄`);
    }
}

function filterAdminProducts() {
    const all = window.adminProductsCache || [];
    const q = (document.getElementById('admin-product-search')?.value || '').trim().toLowerCase();
    const catId = document.getElementById('admin-product-category')?.value || '';
    const brandId = document.getElementById('admin-product-brand')?.value || '';
    const statusFilter = document.getElementById('admin-product-status')?.value || '';

    const filtered = all.filter(p => {
        if (catId && String(p.categoryId) !== String(catId)) return false;
        if (brandId && String(p.brandId) !== String(brandId)) return false;
        const isActive = p.isActive !== false;
        if (statusFilter === 'active' && !isActive) return false;
        if (statusFilter === 'inactive' && isActive) return false;
        if (q) {
            const matchName = (p.name || '').toLowerCase().includes(q)
                || String(p.id) === q
                || (p.productCode || '').toLowerCase().includes(q);
            const matchBrand = (p.brandName || '').toLowerCase().includes(q);
            const matchCat = (p.categoryName || '').toLowerCase().includes(q);
            const matchVariant = (p.variants || []).some(v =>
                (v.variantName || '').toLowerCase().includes(q) ||
                (v.colorName || '').toLowerCase().includes(q)
            );
            const matchSpec = (p.specs || []).some(s =>
                (s.key || '').toLowerCase().includes(q) ||
                (s.value || '').toLowerCase().includes(q)
            );
            if (!matchName && !matchBrand && !matchCat && !matchVariant && !matchSpec) return false;
        }
        return true;
    });

    const totalVariants = filtered.reduce((acc, p) => acc + ((p.variants && p.variants.length) || 0), 0);
    const totalStock = filtered.reduce((acc, p) => acc + (p.stockQuantity || 0), 0);
    const summaryEl = document.getElementById('admin-products-summary');
    if (summaryEl) {
        summaryEl.innerHTML = `Hiển thị <b>${filtered.length}/${all.length}</b> sản phẩm • <b>${totalVariants}</b> biến thể (phiên bản × màu) • Tổng tồn kho: <b>${totalStock}</b> sản phẩm`;
    }

    const tbody = document.getElementById('admin-products-body');
    if (!tbody) return;

    if (!filtered.length) {
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center;padding:32px;color:var(--text-3)">Không tìm thấy sản phẩm phù hợp</td></tr>`;
        return;
    }

    tbody.innerHTML = filtered.map(p => {
        const variants = Array.isArray(p.variants) ? p.variants : [];
        const versions = [...new Set(variants.map(v => v.variantName).filter(Boolean))];
        const colorMap = new Map();
        variants.forEach(v => {
            if (!v.colorName) return;
            if (!colorMap.has(v.colorName)) {
                colorMap.set(v.colorName, {
                    name: v.colorName,
                    hex: v.colorCode || '#64748b',
                    img: v.imageUrl || p.primaryImage || '/images/placeholder.svg',
                    stock: 0
                });
            }
            colorMap.get(v.colorName).stock += (v.stockQuantity || 0);
        });
        const colors = Array.from(colorMap.values());

        const prices = variants.map(v => v.price).filter(pr => pr > 0);
        const minPrice = prices.length ? Math.min(...prices) : (p.salePrice > 0 ? p.salePrice : p.price);
        const maxPrice = prices.length ? Math.max(...prices) : minPrice;
        const inStockVariants = variants.filter(v => (v.stockQuantity || 0) > 0).length;
        const pCode = p.productCode || `SP-${String(p.id).padStart(3, '0')}`;
        const isActive = p.isActive !== false;
        const hlCount = Array.isArray(p.highlights) ? p.highlights.length : 0;
        const specCount = Array.isArray(p.specs) ? p.specs.length : 0;

        return `
        <tr>
            <td>
                <div style="display:flex;flex-direction:column;align-items:center;gap:6px">
                    <a href="/product/${p.id}" target="_blank" title="Xem trang chi tiết sản phẩm">
                        <img src="${p.primaryImage || '/images/placeholder.svg'}" onerror="this.onerror=null;this.src='/images/placeholder.svg';" style="width:52px;height:52px;border-radius:10px;object-fit:contain;border:1.5px solid var(--border);background:#fff">
                    </a>
                    ${colors.length > 0 ? `
                    <div style="display:flex;gap:3px;flex-wrap:wrap;justify-content:center;max-width:82px">
                        ${colors.slice(0, 4).map(c => `
                            <img src="${c.img}" title="${esc(c.name)} (Tồn: ${c.stock})" onerror="this.onerror=null;this.src='/images/placeholder.svg';" style="width:17px;height:17px;border-radius:4px;object-fit:contain;border:1px solid var(--border);background:#fff">
                        `).join('')}
                    </div>` : ''}
                </div>
            </td>
            <td>
                <a href="/product/${p.id}" target="_blank" style="font-weight:700;color:var(--text);text-decoration:none;display:block;line-height:1.35" onmouseover="this.style.color='var(--primary)'" onmouseout="this.style.color='var(--text)'">
                    ${esc(p.name)}
                </a>
                <div style="display:flex;gap:6px;align-items:center;flex-wrap:wrap;margin-top:4px;font-size:11.5px;color:var(--text-3)">
                    <span style="font-family:monospace;background:var(--bg-2);padding:1px 6px;border-radius:4px;border:1px solid var(--border);color:var(--text-2);font-weight:600">${esc(pCode)}</span>
                    <span>•</span>
                    <span style="font-weight:600;color:var(--primary)">${esc(p.brandName || '')}</span>
                    ${p.badge ? `<span class="badge badge-${p.badge.toLowerCase()}" style="font-size:10px;padding:1px 6px">${esc(p.badge)}</span>` : ''}
                </div>
            </td>
            <td>
                <div style="display:flex;flex-direction:column;gap:6px">
                    <div style="display:flex;gap:4px;flex-wrap:wrap">
                        ${versions.length ? versions.map(ver => `
                            <span style="font-size:11px;font-weight:600;padding:2px 7px;border-radius:5px;background:var(--bg-2);border:1px solid var(--border);color:var(--text)">${esc(ver)}</span>
                        `).join('') : '<span style="font-size:11px;color:var(--text-3)">Tiêu chuẩn</span>'}
                    </div>
                    <div style="display:flex;gap:5px;flex-wrap:wrap">
                        ${colors.map(c => `
                            <span style="display:inline-flex;align-items:center;gap:4px;font-size:11px;padding:2px 7px;border-radius:99px;background:#fff;border:1px solid var(--border)" title="Màu ${esc(c.name)}: còn ${c.stock} SP">
                                <span style="width:9px;height:9px;border-radius:50%;background:${c.hex};border:1px solid rgba(0,0,0,0.2);display:inline-block"></span>
                                <span>${esc(c.name)}</span>
                                <b style="color:${c.stock > 0 ? 'var(--primary)' : 'var(--danger)'}">(${c.stock})</b>
                            </span>
                        `).join('')}
                    </div>
                </div>
            </td>
            <td>
                <div style="font-weight:700;color:var(--primary);font-size:13.5px">${fmt(minPrice)}${maxPrice > minPrice ? ` - ${fmt(maxPrice)}` : ''}</div>
                ${p.price > minPrice ? `<div style="font-size:11.5px;color:var(--text-3);text-decoration:line-through">${fmt(p.price)}</div>` : ''}
            </td>
            <td>
                <div style="font-size:12.5px;font-weight:700;color:var(--text)">${esc(p.categoryName)}</div>
                <div style="display:flex;gap:4px;flex-wrap:wrap;margin-top:4px">
                    <span style="font-size:10.5px;padding:1px 6px;border-radius:4px;background:#eef2ff;color:var(--primary)" title="Số đặc điểm nổi bật">🌟 ${hlCount} nổi bật</span>
                    <span style="font-size:10.5px;padding:1px 6px;border-radius:4px;background:#f0fdf4;color:#15803d" title="Số thông số kỹ thuật">📋 ${specCount} thông số</span>
                </div>
            </td>
            <td>
                <div style="display:flex;gap:4px;align-items:center;flex-wrap:wrap">
                    ${p.stockQuantity <= 0 ? `<span class="badge badge-hot">Hết hàng (0)</span>`
                      : p.stockQuantity < 10 ? `<span class="badge" style="background:#fef3c7;color:#d97706">Sắp hết (${p.stockQuantity})</span>`
                      : `<span class="badge badge-new">Còn hàng (${p.stockQuantity})</span>`}
                    ${!isActive ? `<span class="badge" style="background:#f1f5f9;color:#64748b">Tạm ẩn</span>` : ''}
                </div>
                <div style="font-size:11px;color:var(--text-3);margin-top:4px">${inStockVariants}/${variants.length || 1} biến thể còn hàng</div>
            </td>
            <td>
                <div style="display:flex;gap:5px;align-items:center">
                    <a href="/product/${p.id}" target="_blank" class="btn btn-ghost btn-sm" title="Mở trang chi tiết sản phẩm" style="text-decoration:none;padding:5px 8px">👁️</a>
                    <button class="btn btn-outline btn-sm" onclick="editProduct(${p.id})" title="Chỉnh sửa thông tin, hình ảnh, đặc điểm nổi bật & thông số kỹ thuật" style="padding:5px 9px">✏️ Sửa</button>
                    <button class="btn btn-ghost btn-sm" style="color:var(--danger);padding:5px 8px" onclick="deleteProduct(${p.id})" title="Xóa sản phẩm">🗑️</button>
                </div>
            </td>
        </tr>`;
    }).join('');
}

let activeVariantImageRowBtn = null;

function addProductVariantRow(v = null) {
    const tbody = document.getElementById('cp-variants-tbody');
    if (!tbody) return;

    const basePrice = Number(document.getElementById('cp-saleprice')?.value) > 0
        ? Number(document.getElementById('cp-saleprice')?.value)
        : (Number(document.getElementById('cp-price')?.value) || 0);
    const defaultImg = document.getElementById('cp-image')?.value?.trim() || '';

    const variantId = v?.id ?? 0;
    const variantName = v?.variantName ?? 'Tiêu chuẩn';
    const colorName = v?.colorName ?? 'Đen';
    const colorCode = v?.colorCode || '#1e293b';
    const price = v?.price ?? basePrice;
    const stock = v?.stockQuantity ?? 10;
    const imageUrl = v?.imageUrl ?? defaultImg;
    const sku = v?.sku ?? '';

    const tr = document.createElement('tr');
    tr.className = 'cp-variant-row';
    tr.style.borderBottom = '1px solid var(--border)';
    tr.innerHTML = `
        <td style="padding: 6px 4px;">
            <input type="hidden" class="cp-var-id" value="${variantId}">
            <input type="hidden" class="cp-var-sku" value="${esc(sku)}">
            <input type="text" class="cp-var-name" value="${esc(variantName)}" placeholder="VD: 256GB" required
                   style="width:100%;padding:6px 8px;border:1px solid var(--border);border-radius:6px;font-size:12.5px">
        </td>
        <td style="padding: 6px 4px;">
            <input type="text" class="cp-var-color" value="${esc(colorName)}" placeholder="VD: Titan Đen" required
                   oninput="onVariantColorNameChange(this)"
                   style="width:100%;padding:6px 8px;border:1px solid var(--border);border-radius:6px;font-size:12.5px">
        </td>
        <td style="padding: 6px 4px; text-align:center;">
            <input type="color" class="cp-var-hex" value="${colorCode.startsWith('#') && colorCode.length === 7 ? colorCode : '#334155'}"
                   oninput="syncSameColorHex(this)"
                   style="width:36px;height:30px;padding:1px;border:1px solid var(--border);border-radius:6px;cursor:pointer;background:#fff">
        </td>
        <td style="padding: 6px 4px;">
            <input type="number" class="cp-var-price" value="${price}" min="0" required
                   style="width:100%;padding:6px 8px;border:1px solid var(--border);border-radius:6px;font-size:12.5px">
        </td>
        <td style="padding: 6px 4px;">
            <input type="number" class="cp-var-stock" value="${stock}" min="0" required
                   oninput="recalcTotalStockFromVariants()"
                   style="width:100%;padding:6px 8px;border:1px solid var(--border);border-radius:6px;font-size:12.5px;font-weight:700">
        </td>
        <td style="padding: 6px 4px;">
            <div style="display:flex;align-items:center;gap:6px">
                <img class="cp-var-img-preview" src="${imageUrl || '/images/placeholder.svg'}"
                     onerror="this.onerror=null;this.src='/images/placeholder.svg';"
                     style="width:30px;height:30px;border-radius:6px;object-fit:cover;border:1px solid var(--border);background:#fff;flex-shrink:0">
                <input type="text" class="cp-var-img" value="${esc(imageUrl)}" placeholder="/images/products/..."
                       oninput="onVariantImageInputChange(this)"
                       style="flex:1;min-width:90px;padding:6px 8px;border:1px solid var(--border);border-radius:6px;font-size:12px">
                <button type="button" class="btn btn-outline btn-sm" onclick="triggerVariantImageUpload(this)" title="Tải ảnh cho màu này từ máy" style="padding:5px 8px;flex-shrink:0">📁</button>
            </div>
        </td>
        <td style="padding: 6px 2px; text-align:center;">
            <button type="button" class="btn btn-ghost btn-sm" onclick="removeProductVariantRow(this)" style="color:var(--danger);padding:4px 7px" title="Xóa biến thể này">✕</button>
        </td>
    `;
    tbody.appendChild(tr);
    recalcTotalStockFromVariants();
}

function removeProductVariantRow(btn) {
    const tbody = document.getElementById('cp-variants-tbody');
    if (!tbody) return;
    if (tbody.querySelectorAll('.cp-variant-row').length <= 1) {
        return toast('Sản phẩm cần có ít nhất 1 biến thể!', 'error');
    }
    btn.closest('.cp-variant-row')?.remove();
    recalcTotalStockFromVariants();
}

function recalcTotalStockFromVariants() {
    const rows = document.querySelectorAll('#cp-variants-tbody .cp-variant-row');
    let total = 0;
    rows.forEach(r => {
        const val = parseInt(r.querySelector('.cp-var-stock')?.value || '0', 10);
        if (!isNaN(val) && val > 0) total += val;
    });
    const stockEl = document.getElementById('cp-stock');
    if (stockEl) stockEl.value = total;
}

function onVariantImageInputChange(input) {
    const row = input.closest('.cp-variant-row');
    if (!row) return;
    const url = input.value.trim();
    const preview = row.querySelector('.cp-var-img-preview');
    if (preview) preview.src = url || '/images/placeholder.svg';

    const colorName = row.querySelector('.cp-var-color')?.value?.trim().toLowerCase();
    if (colorName) {
        document.querySelectorAll('#cp-variants-tbody .cp-variant-row').forEach(otherRow => {
            if (otherRow === row) return;
            const otherColor = otherRow.querySelector('.cp-var-color')?.value?.trim().toLowerCase();
            if (otherColor === colorName) {
                const otherInput = otherRow.querySelector('.cp-var-img');
                const otherPreview = otherRow.querySelector('.cp-var-img-preview');
                if (otherInput) otherInput.value = url;
                if (otherPreview) otherPreview.src = url || '/images/placeholder.svg';
            }
        });
    }
}

function syncSameColorHex(hexInput) {
    const row = hexInput.closest('.cp-variant-row');
    if (!row) return;
    const colorName = row.querySelector('.cp-var-color')?.value?.trim().toLowerCase();
    if (!colorName) return;
    document.querySelectorAll('#cp-variants-tbody .cp-variant-row').forEach(otherRow => {
        if (otherRow === row) return;
        const otherColor = otherRow.querySelector('.cp-var-color')?.value?.trim().toLowerCase();
        if (otherColor === colorName) {
            const otherHex = otherRow.querySelector('.cp-var-hex');
            if (otherHex) otherHex.value = hexInput.value;
        }
    });
}

function onVariantColorNameChange(colorInput) {
    const row = colorInput.closest('.cp-variant-row');
    if (!row) return;
    const colorName = colorInput.value.trim().toLowerCase();
    if (!colorName) return;
    document.querySelectorAll('#cp-variants-tbody .cp-variant-row').forEach(otherRow => {
        if (otherRow === row) return;
        const otherColor = otherRow.querySelector('.cp-var-color')?.value?.trim().toLowerCase();
        if (otherColor === colorName) {
            const existingImg = otherRow.querySelector('.cp-var-img')?.value?.trim();
            const existingHex = otherRow.querySelector('.cp-var-hex')?.value;
            const myImgInput = row.querySelector('.cp-var-img');
            const myPreview = row.querySelector('.cp-var-img-preview');
            const myHex = row.querySelector('.cp-var-hex');
            if (existingImg && myImgInput && !myImgInput.value.trim()) {
                myImgInput.value = existingImg;
                if (myPreview) myPreview.src = existingImg;
            }
            if (existingHex && myHex) myHex.value = existingHex;
        }
    });
}

function triggerVariantImageUpload(btn) {
    activeVariantImageRowBtn = btn;
    const fileInput = document.getElementById('cp-variant-file-input');
    if (fileInput) {
        fileInput.value = '';
        fileInput.click();
    }
}

async function handleVariantImageUpload(input) {
    const file = input.files?.[0];
    if (!file || !activeVariantImageRowBtn) return;

    const row = activeVariantImageRowBtn.closest('.cp-variant-row');
    if (!row) return;
    const imgInput = row.querySelector('.cp-var-img');
    const preview = row.querySelector('.cp-var-img-preview');
    if (preview) preview.src = URL.createObjectURL(file);

    const formData = new FormData();
    formData.append('file', file);

    try {
        const headers = {};
        if (authToken) headers['Authorization'] = 'Bearer ' + authToken;
        const res = await fetch(API + '/api/upload/image', {
            method: 'POST',
            headers: headers,
            body: formData
        });
        const data = await res.json();
        if (res.ok && data.url) {
            if (imgInput) {
                imgInput.value = data.url;
                onVariantImageInputChange(imgInput);
            }
            const mainImg = document.getElementById('cp-image');
            if (mainImg && !mainImg.value.trim()) {
                mainImg.value = data.url;
            }
            toast('Đã tải ảnh màu sắc thành công! 🎨');
        } else {
            toast(data.message || 'Lỗi tải ảnh màu', 'error');
        }
    } catch (err) {
        toast('Lỗi kết nối khi tải ảnh màu', 'error');
    }
}

async function handleImageUpload(input) {
    const file = input.files?.[0];
    if (!file) return;

    const preview = document.getElementById('cp-image-preview');
    const previewWrap = document.getElementById('cp-image-preview-wrap');
    const statusText = document.getElementById('cp-image-status');
    const imageInput = document.getElementById('cp-image');

    preview.src = URL.createObjectURL(file);
    previewWrap.style.display = 'flex';
    statusText.textContent = 'Đang tải lên server...';
    statusText.style.color = 'var(--primary)';

    const formData = new FormData();
    formData.append('file', file);

    try {
        const headers = {};
        if (authToken) headers['Authorization'] = 'Bearer ' + authToken;
        const res = await fetch(API + '/api/upload/image', {
            method: 'POST',
            headers: headers,
            body: formData
        });
        const data = await res.json();
        if (res.ok && data.url) {
            imageInput.value = data.url;
            statusText.textContent = '✓ Đã lưu vào máy thành công!';
            statusText.style.color = 'var(--success)';
            toast('Đã lưu ảnh vào máy! 🖼️');
        } else {
            statusText.textContent = data.message || 'Lỗi tải ảnh';
            statusText.style.color = 'var(--danger)';
            toast(data.message || 'Lỗi tải ảnh', 'error');
        }
    } catch (err) {
        statusText.textContent = 'Lỗi kết nối';
        statusText.style.color = 'var(--danger)';
        toast('Lỗi khi tải ảnh lên máy', 'error');
    }
}

function clearProductImage() {
    document.getElementById('cp-image').value = '';
    document.getElementById('cp-file-input').value = '';
    document.getElementById('cp-image-preview').src = '';
    document.getElementById('cp-image-preview-wrap').style.display = 'none';
    document.getElementById('cp-image-status').textContent = '';
}

function onPrimaryImageInputChange(url) {
    const val = (url || '').trim();
    const previewWrap = document.getElementById('cp-image-preview-wrap');
    const preview = document.getElementById('cp-image-preview');
    const statusText = document.getElementById('cp-image-status');
    if (val) {
        preview.src = val;
        previewWrap.style.display = 'flex';
        statusText.textContent = 'Ảnh đại diện hiện tại';
        statusText.style.color = 'var(--primary)';
    } else {
        previewWrap.style.display = 'none';
    }
}

let appBrands = [];
let appCategories = [];
let currentProductStep = 1;
let currentProductGalleryUrls = [];

const DEFAULT_SPEC_TEMPLATES_BY_CAT = {
    1: ['Màn hình', 'Bộ vi xử lý', 'RAM', 'Bộ nhớ trong', 'Camera', 'Dung lượng pin', 'Hệ điều hành', 'Kết nối'],
    2: ['CPU', 'RAM', 'Ổ cứng', 'Card đồ họa', 'Kích thước màn hình', 'Độ phân giải', 'Hệ điều hành', 'Dung lượng pin'],
    3: ['Loại tai nghe', 'Kết nối', 'Thời lượng pin', 'Công nghệ chống ồn', 'Chuẩn chống nước', 'Micro', 'Cổng sạc'],
    4: ['Kích thước màn hình', 'Công nghệ màn hình', 'Thời lượng pin', 'Cảm biến', 'Khả năng chống nước', 'Kết nối', 'Hệ điều hành tương thích'],
    5: ['Màn hình', 'Bộ vi xử lý', 'RAM', 'Bộ nhớ trong', 'Camera', 'Dung lượng pin', 'Hệ điều hành', 'Kết nối'],
    6: ['Loại phụ kiện', 'Công suất / Dung lượng', 'Chuẩn kết nối', 'Thiết bị tương thích', 'Chất liệu / Kích thước']
};

function getSpecTemplatesForCategory(catId) {
    const cat = appCategories.find(c => Number(c.id) === Number(catId));
    if (cat && Array.isArray(cat.specTemplates) && cat.specTemplates.length > 0) {
        return cat.specTemplates;
    }
    return DEFAULT_SPEC_TEMPLATES_BY_CAT[Number(catId)] || ['Loại sản phẩm', 'Thương hiệu', 'Kết nối', 'Bảo hành'];
}

function switchProductModalTab(step) {
    currentProductStep = Math.max(1, Math.min(4, Number(step) || 1));
    for (let i = 1; i <= 4; i++) {
        const pane = document.getElementById(`cp-step-${i}`);
        if (pane) pane.style.display = i === currentProductStep ? '' : 'none';
    }
    document.querySelectorAll('#cp-tabs-nav .cp-tab-btn').forEach(btn => {
        const s = Number(btn.getAttribute('data-step'));
        const isActive = s === currentProductStep;
        btn.classList.toggle('active', isActive);
        btn.style.background = isActive ? 'var(--primary)' : 'transparent';
        btn.style.color = isActive ? '#fff' : 'var(--text-2)';
        btn.style.fontWeight = isActive ? '700' : '600';
        const circle = btn.querySelector('span:first-child');
        if (circle) {
            circle.style.background = isActive ? 'rgba(255,255,255,.22)' : 'var(--border)';
        }
    });
    const prevBtn = document.getElementById('cp-prev-step-btn');
    const nextBtn = document.getElementById('cp-next-step-btn');
    if (prevBtn) prevBtn.style.display = currentProductStep > 1 ? '' : 'none';
    if (nextBtn) nextBtn.style.display = currentProductStep < 4 ? '' : 'none';
}

function stepProductModal(delta) {
    switchProductModalTab(currentProductStep + delta);
}

/* GALLERY IMAGES IN STEP 2 */
function renderProductGalleryList() {
    const container = document.getElementById('cp-gallery-list');
    if (!container) return;
    if (!currentProductGalleryUrls.length) {
        container.innerHTML = `<div style="grid-column:1/-1;padding:18px;text-align:center;color:var(--text-3);font-size:12.5px;border:1px dashed var(--border);border-radius:10px;background:var(--bg-card)">Chưa có ảnh chi tiết bổ sung. Hệ thống sẽ tự động lấy ảnh đại diện và ảnh các màu sắc ở Bước 1 làm bộ sưu tập ảnh.</div>`;
        return;
    }
    container.innerHTML = currentProductGalleryUrls.map((url, idx) => `
        <div style="position:relative;border:1px solid var(--border);border-radius:10px;background:var(--bg-card);padding:6px;display:flex;flex-direction:column;gap:6px">
            <img src="${esc(url)}" onerror="this.src='/images/placeholder.svg'" style="width:100%;height:86px;object-fit:contain;border-radius:6px;background:#fff">
            <div style="font-size:10.5px;color:var(--text-3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis" title="${esc(url)}">${esc(url)}</div>
            <button type="button" onclick="removeGalleryUrl(${idx})" style="position:absolute;top:4px;right:4px;width:22px;height:22px;border-radius:50%;border:none;background:rgba(239,68,68,.9);color:#fff;font-size:11px;cursor:pointer;display:flex;align-items:center;justify-content:center" title="Xóa ảnh này">✕</button>
        </div>
    `).join('');
}

function addGalleryUrlFromInput() {
    const inp = document.getElementById('cp-gallery-url-input');
    const url = (inp?.value || '').trim();
    if (!url) return;
    if (!currentProductGalleryUrls.includes(url)) {
        currentProductGalleryUrls.push(url);
        renderProductGalleryList();
    }
    inp.value = '';
}

function removeGalleryUrl(idx) {
    currentProductGalleryUrls.splice(idx, 1);
    renderProductGalleryList();
}

async function handleGalleryFileUpload(input) {
    const files = Array.from(input.files || []);
    if (!files.length) return;
    for (const file of files) {
        const formData = new FormData();
        formData.append('file', file);
        try {
            const headers = {};
            if (authToken) headers['Authorization'] = 'Bearer ' + authToken;
            const res = await fetch(API + '/api/upload/image', {
                method: 'POST',
                headers,
                body: formData
            });
            const data = await res.json();
            if (res.ok && data.url) {
                if (!currentProductGalleryUrls.includes(data.url)) {
                    currentProductGalleryUrls.push(data.url);
                }
                const mainImg = document.getElementById('cp-image');
                if (mainImg && !mainImg.value.trim()) {
                    mainImg.value = data.url;
                    onPrimaryImageInputChange(data.url);
                }
            }
        } catch (err) {
            console.error(err);
        }
    }
    input.value = '';
    renderProductGalleryList();
    toast('Đã tải ảnh chi tiết lên bộ sưu tập! 📸');
}

/* HIGHLIGHTS IN STEP 3 */
function addProductHighlightRow(val = '') {
    const list = document.getElementById('cp-highlights-list');
    if (!list) return;
    const row = document.createElement('div');
    row.className = 'cp-highlight-row';
    row.style.cssText = 'display:flex;align-items:center;gap:8px';
    row.innerHTML = `
        <span style="color:var(--primary);font-weight:700;font-size:15px">✓</span>
        <input type="text" class="cp-highlight-input" value="${esc(val)}" placeholder="Nhập đặc điểm nổi bật (VD: Chống ồn chủ động ANC thế hệ mới, Thời lượng pin 30 giờ...)"
               style="flex:1;padding:8px 12px;border:1px solid var(--border);border-radius:8px;font-size:13px">
        <button type="button" class="btn btn-ghost btn-sm" onclick="this.closest('.cp-highlight-row').remove()" style="color:var(--danger);padding:4px 8px" title="Xóa dòng này">✕</button>
    `;
    list.appendChild(row);
}

function collectProductHighlightsFromForm() {
    const inputs = document.querySelectorAll('#cp-highlights-list .cp-highlight-input');
    const result = [];
    inputs.forEach(inp => {
        const v = inp.value.trim();
        if (v) result.push(v);
    });
    return result;
}

/* DYNAMIC CATEGORY SPECS IN STEP 4 */
function addProductSpecRow(key = '', val = '') {
    const tbody = document.getElementById('cp-specs-tbody');
    if (!tbody) return;
    const tr = document.createElement('tr');
    tr.className = 'cp-spec-row';
    tr.style.borderBottom = '1px solid var(--border)';
    tr.innerHTML = `
        <td style="padding:6px 4px">
            <input type="text" class="cp-spec-key" value="${esc(key)}" placeholder="Tên thông số (VD: Màn hình, Thời lượng pin...)"
                   style="width:100%;padding:7px 10px;border:1px solid var(--border);border-radius:7px;font-size:13px;font-weight:600">
        </td>
        <td style="padding:6px 4px">
            <input type="text" class="cp-spec-val" value="${esc(val)}" placeholder="Nhập giá trị thông số (để trống nếu sản phẩm không có)"
                   style="width:100%;padding:7px 10px;border:1px solid var(--border);border-radius:7px;font-size:13px">
        </td>
        <td style="padding:6px 2px;text-align:center">
            <button type="button" class="btn btn-ghost btn-sm" onclick="this.closest('.cp-spec-row').remove()" style="color:var(--danger);padding:4px 7px" title="Xóa thông số này">✕</button>
        </td>
    `;
    tbody.appendChild(tr);
}

function collectProductSpecsFromForm() {
    const rows = document.querySelectorAll('#cp-specs-tbody .cp-spec-row');
    const result = [];
    rows.forEach(r => {
        const k = r.querySelector('.cp-spec-key')?.value?.trim() || '';
        const v = r.querySelector('.cp-spec-val')?.value?.trim() || '';
        if (k && v) {
            result.push({ key: k, value: v });
        }
    });
    return result;
}

function applyCategorySpecTemplate(preserveExisting = true) {
    const catSelect = document.getElementById('cp-category');
    const catId = Number(catSelect?.value || 1);
    const catObj = appCategories.find(c => Number(c.id) === catId);
    const badge = document.getElementById('cp-spec-cat-badge');
    if (badge) badge.textContent = catObj ? catObj.name : (catSelect?.options[catSelect.selectedIndex]?.text || 'Danh mục');

    const templates = getSpecTemplatesForCategory(catId);
    const existingMap = new Map();
    if (preserveExisting) {
        document.querySelectorAll('#cp-specs-tbody .cp-spec-row').forEach(r => {
            const k = r.querySelector('.cp-spec-key')?.value?.trim();
            const v = r.querySelector('.cp-spec-val')?.value?.trim();
            if (k && v) existingMap.set(k.toLowerCase(), { key: k, value: v });
        });
    }

    const tbody = document.getElementById('cp-specs-tbody');
    if (!tbody) return;
    tbody.innerHTML = '';

    const usedKeys = new Set();
    templates.forEach(tKey => {
        const lower = tKey.toLowerCase();
        const match = existingMap.get(lower);
        usedKeys.add(lower);
        addProductSpecRow(tKey, match ? match.value : '');
    });

    // Giữ lại các thông số có giá trị mà người dùng đã nhập thêm
    if (preserveExisting) {
        existingMap.forEach((item, lower) => {
            if (!usedKeys.has(lower)) {
                addProductSpecRow(item.key, item.value);
            }
        });
    }
}

function onProductCategoryChange() {
    // Khi đổi danh mục sản phẩm: tự động cập nhật khung thông số kỹ thuật theo danh mục mới
    // Chỉ giữ lại các giá trị nếu cùng tên trường thuộc danh mục mới, loại bỏ các trường trống của danh mục cũ
    const catSelect = document.getElementById('cp-category');
    const catId = Number(catSelect?.value || 1);
    const catObj = appCategories.find(c => Number(c.id) === catId);
    const badge = document.getElementById('cp-spec-cat-badge');
    if (badge) badge.textContent = catObj ? catObj.name : (catSelect?.options[catSelect.selectedIndex]?.text || 'Danh mục');

    const templates = getSpecTemplatesForCategory(catId);
    const currentValues = new Map();
    document.querySelectorAll('#cp-specs-tbody .cp-spec-row').forEach(r => {
        const k = r.querySelector('.cp-spec-key')?.value?.trim();
        const v = r.querySelector('.cp-spec-val')?.value?.trim();
        if (k && v) currentValues.set(k.toLowerCase(), v);
    });

    const tbody = document.getElementById('cp-specs-tbody');
    if (!tbody) return;
    tbody.innerHTML = '';
    templates.forEach(tKey => {
        const val = currentValues.get(tKey.toLowerCase()) || '';
        addProductSpecRow(tKey, val);
    });
}

async function loadBrandsAndCategories() {
    try {
        const [bRes, cRes] = await Promise.all([
            api('GET', '/api/brands'),
            api('GET', '/api/categories')
        ]);
        if (bRes.ok && Array.isArray(bRes.data)) {
            appBrands = bRes.data;
            const cpBrand = document.getElementById('cp-brand');
            if (cpBrand) {
                const curVal = cpBrand.value;
                cpBrand.innerHTML = appBrands.map(b => `<option value="${b.id}">${esc(b.name)}</option>`).join('');
                if (curVal) cpBrand.value = curVal;
            }
            const brandSelect = document.getElementById('brand-select');
            if (brandSelect) {
                const urlBrand = new URLSearchParams(window.location.search).get('brand');
                const currentVal = brandSelect.value || urlBrand;
                brandSelect.innerHTML = `<option value="">🏷️ Tất cả thương hiệu</option>` +
                    appBrands.map(b => `<option value="${b.name}">${b.name}</option>`).join('');
                if (currentVal) brandSelect.value = currentVal;
            }
        }
        if (cRes.ok && Array.isArray(cRes.data)) {
            appCategories = cRes.data;
            const cpCat = document.getElementById('cp-category');
            if (cpCat) {
                const curVal = cpCat.value;
                cpCat.innerHTML = appCategories.map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
                if (curVal) cpCat.value = curVal;
            }
        }
    } catch (e) {
        console.error('Error loading brands/categories:', e);
    }
}

async function openNewProductModal() {
    await loadBrandsAndCategories();
    document.getElementById('cp-id').value = '';
    document.getElementById('cp-name').value = '';
    const codeEl = document.getElementById('cp-code');
    if (codeEl) codeEl.value = 'Tự động sinh khi tạo';
    const badgeEl = document.getElementById('cp-badge');
    if (badgeEl) badgeEl.value = '';
    const activeEl = document.getElementById('cp-active');
    if (activeEl) activeEl.value = 'true';
    document.getElementById('cp-image').value = '';
    document.getElementById('cp-price').value = '';
    document.getElementById('cp-saleprice').value = '0';
    if (appCategories.length) document.getElementById('cp-category').value = String(appCategories[0].id);
    if (appBrands.length) document.getElementById('cp-brand').value = String(appBrands[0].id);
    document.getElementById('cp-stock').value = '10';
    document.getElementById('cp-desc').value = '';
    document.getElementById('cp-file-input').value = '';
    document.getElementById('cp-image-preview-wrap').style.display = 'none';
    document.getElementById('cp-image-preview').src = '';
    document.getElementById('cp-image-status').textContent = '';

    currentProductGalleryUrls = [];
    renderProductGalleryList();

    const hlList = document.getElementById('cp-highlights-list');
    if (hlList) {
        hlList.innerHTML = '';
        addProductHighlightRow('');
        addProductHighlightRow('');
        addProductHighlightRow('');
    }

    const tbody = document.getElementById('cp-variants-tbody');
    if (tbody) {
        tbody.innerHTML = '';
        addProductVariantRow({
            variantName: 'Tiêu chuẩn',
            colorName: 'Đen',
            colorCode: '#1e293b',
            price: 0,
            stockQuantity: 10,
            imageUrl: ''
        });
    }

    onProductCategoryChange();
    switchProductModalTab(1);

    document.querySelector('#create-product-modal .modal-title').textContent = '✨ Thêm Sản Phẩm Mới';
    document.getElementById('cp-submit-btn').textContent = '💾 Lưu & Đồng Bộ Sản Phẩm';
    openModal('create-product-modal');
}

async function editProduct(id) {
    await loadBrandsAndCategories();
    const res = await api('GET', `/api/products/${id}`);
    let p = (res.ok && res.data) ? res.data : window.adminProductsCache?.find(x => x.id === id);
    if (!p) return;

    document.getElementById('cp-id').value = p.id;
    document.getElementById('cp-name').value = p.name || '';
    const codeEl = document.getElementById('cp-code');
    if (codeEl) codeEl.value = `#${p.id} • ${p.productCode || 'SP-' + String(p.id).padStart(3, '0')}`;
    const badgeEl = document.getElementById('cp-badge');
    if (badgeEl) badgeEl.value = p.badge || '';
    const activeEl = document.getElementById('cp-active');
    if (activeEl) activeEl.value = p.isActive === false ? 'false' : 'true';
    document.getElementById('cp-image').value = p.primaryImage || '';
    document.getElementById('cp-price').value = p.price;
    document.getElementById('cp-saleprice').value = p.salePrice || 0;

    const catSelect = document.getElementById('cp-category');
    if (catSelect) catSelect.value = String(p.categoryId);
    const brandSelect = document.getElementById('cp-brand');
    if (brandSelect) brandSelect.value = String(p.brandId);

    document.getElementById('cp-stock').value = p.stockQuantity;
    document.getElementById('cp-desc').value = p.description || '';
    document.getElementById('cp-file-input').value = '';

    if (p.primaryImage) {
        document.getElementById('cp-image-preview').src = p.primaryImage;
        document.getElementById('cp-image-preview-wrap').style.display = 'flex';
        document.getElementById('cp-image-status').textContent = 'Ảnh đại diện hiện tại';
        document.getElementById('cp-image-status').style.color = 'var(--text-3)';
    } else {
        document.getElementById('cp-image-preview-wrap').style.display = 'none';
    }

    // Gallery images
    currentProductGalleryUrls = Array.isArray(p.images) ? [...p.images] : (p.primaryImage ? [p.primaryImage] : []);
    renderProductGalleryList();

    // Highlights
    const hlList = document.getElementById('cp-highlights-list');
    if (hlList) {
        hlList.innerHTML = '';
        if (Array.isArray(p.highlights) && p.highlights.length > 0) {
            p.highlights.forEach(h => addProductHighlightRow(h));
        } else {
            addProductHighlightRow('');
            addProductHighlightRow('');
        }
    }

    // Variants
    const tbody = document.getElementById('cp-variants-tbody');
    if (tbody) {
        tbody.innerHTML = '';
        if (Array.isArray(p.variants) && p.variants.length > 0) {
            p.variants.forEach(v => addProductVariantRow(v));
        } else {
            addProductVariantRow({
                variantName: 'Tiêu chuẩn',
                colorName: 'Mặc định',
                colorCode: '#334155',
                price: p.salePrice > 0 ? p.salePrice : p.price,
                stockQuantity: p.stockQuantity || 10,
                imageUrl: p.primaryImage || ''
            });
        }
    }

    // Specs (merge existing product specs with category template)
    const specsTbody = document.getElementById('cp-specs-tbody');
    if (specsTbody) {
        specsTbody.innerHTML = '';
        const catObj = appCategories.find(c => Number(c.id) === Number(p.categoryId));
        const badge = document.getElementById('cp-spec-cat-badge');
        if (badge) badge.textContent = catObj ? catObj.name : (p.categoryName || 'Danh mục');

        const templates = getSpecTemplatesForCategory(p.categoryId);
        const prodSpecs = Array.isArray(p.specs) ? p.specs : [];
        const specMap = new Map();
        prodSpecs.forEach(s => {
            if (s.key) specMap.set(s.key.trim().toLowerCase(), s);
        });

        const used = new Set();
        templates.forEach(tKey => {
            const lower = tKey.toLowerCase();
            const found = specMap.get(lower);
            used.add(lower);
            addProductSpecRow(tKey, found ? found.value : '');
        });
        prodSpecs.forEach(s => {
            const lower = (s.key || '').trim().toLowerCase();
            if (lower && !used.has(lower)) {
                addProductSpecRow(s.key, s.value);
            }
        });
    }

    switchProductModalTab(1);
    document.querySelector('#create-product-modal .modal-title').textContent = `✏️ Chỉnh Sửa Sản Phẩm #${p.id} (${p.productCode || 'SP-' + p.id})`;
    document.getElementById('cp-submit-btn').textContent = '💾 Cập Nhật & Đồng Bộ Hệ Thống';
    openModal('create-product-modal');
}

async function deleteProduct(id) {
    if (!confirm('Bạn có chắc muốn xóa sản phẩm này cùng toàn bộ biến thể, hình ảnh, đặc điểm nổi bật và thông số khỏi hệ thống không?')) return;
    const { ok } = await api('DELETE', `/api/products/${id}`);
    if (ok) {
        toast('Đã xóa sản phẩm thành công! 🗑️');
        loadAdminProducts();
    } else {
        toast('Lỗi khi xóa sản phẩm', 'error');
    }
}

async function submitCreateProduct(e) {
    e.preventDefault();
    const nameVal = document.getElementById('cp-name').value.trim();
    const priceVal = Number(document.getElementById('cp-price').value) || 0;
    if (!nameVal || priceVal <= 0) {
        switchProductModalTab(1);
        return toast('Vui lòng nhập đầy đủ Tên sản phẩm và Giá gốc hợp lệ ở Bước 1!', 'error');
    }

    const btn = document.getElementById('cp-submit-btn');
    btn.textContent = 'Đang đồng bộ...'; btn.disabled = true;

    const id = document.getElementById('cp-id').value;
    const basePrice = priceVal;
    const salePrice = Number(document.getElementById('cp-saleprice').value) || 0;
    const effectiveBase = salePrice > 0 ? salePrice : basePrice;
    let primaryImage = document.getElementById('cp-image').value.trim();

    const variantRows = document.querySelectorAll('#cp-variants-tbody .cp-variant-row');
    const variants = [];
    variantRows.forEach(r => {
        const varId = parseInt(r.querySelector('.cp-var-id')?.value || '0', 10) || 0;
        const variantName = r.querySelector('.cp-var-name')?.value?.trim() || 'Tiêu chuẩn';
        const colorName = r.querySelector('.cp-var-color')?.value?.trim() || 'Mặc định';
        const colorCode = r.querySelector('.cp-var-hex')?.value || '#334155';
        const vPriceRaw = Number(r.querySelector('.cp-var-price')?.value) || 0;
        const vPrice = vPriceRaw > 0 ? vPriceRaw : effectiveBase;
        const vStock = Math.max(0, parseInt(r.querySelector('.cp-var-stock')?.value || '0', 10) || 0);
        const vImg = r.querySelector('.cp-var-img')?.value?.trim() || primaryImage;
        const sku = r.querySelector('.cp-var-sku')?.value?.trim() || '';

        if (!primaryImage && vImg) primaryImage = vImg;

        variants.push({
            id: varId,
            variantName,
            colorName,
            colorCode,
            price: vPrice,
            originalPrice: Math.max(basePrice, vPrice),
            stockQuantity: vStock,
            imageUrl: vImg,
            sku
        });
    });

    if (!primaryImage && currentProductGalleryUrls.length > 0) {
        primaryImage = currentProductGalleryUrls[0];
    }

    const totalStock = variants.length
        ? variants.reduce((s, v) => s + v.stockQuantity, 0)
        : (Number(document.getElementById('cp-stock').value) || 0);

    const highlights = collectProductHighlightsFromForm();
    const specs = collectProductSpecsFromForm();

    const body = {
        name: nameVal,
        productCode: document.getElementById('cp-code')?.value?.trim() || null,
        primaryImage,
        price: basePrice,
        salePrice: salePrice,
        categoryId: Number(document.getElementById('cp-category').value),
        brandId: Number(document.getElementById('cp-brand').value),
        stockQuantity: totalStock,
        description: document.getElementById('cp-desc').value.trim(),
        badge: document.getElementById('cp-badge')?.value || '',
        isActive: document.getElementById('cp-active')?.value !== 'false',
        images: currentProductGalleryUrls,
        highlights,
        specs,
        variants
    };

    let ok = false;
    let resData = null;
    if (id) {
        const res = await api('PUT', `/api/products/${id}`, body);
        ok = res.ok;
        resData = res.data;
    } else {
        const res = await api('POST', '/api/products', body);
        ok = res.ok;
        resData = res.data;
    }

    btn.textContent = id ? '💾 Cập Nhật & Đồng Bộ Hệ Thống' : '💾 Lưu & Đồng Bộ Sản Phẩm';
    btn.disabled = false;

    if (!ok) {
        const errMsg = resData?.message || (resData?.errors ? Object.values(resData.errors).flat().join(', ') : 'Lỗi khi lưu sản phẩm');
        return toast(errMsg, 'error');
    }

    toast(id ? 'Đã cập nhật & đồng bộ sản phẩm thành công! 🎉' : 'Đã thêm sản phẩm mới thành công! 🎉');
    closeModal('create-product-modal');
    e.target.reset();
    document.getElementById('cp-id').value = '';
    await loadAdminProducts();
    if (typeof loadProducts === 'function' && document.getElementById('products-grid')) {
        loadProducts();
    }
}

/* ADMIN CATEGORY MANAGEMENT */
async function loadAdminCategories(isManualRefresh = false, btnEl = null) {
    if (isManualRefresh) {
        const sEl = document.getElementById('admin-category-search');
        const stEl = document.getElementById('admin-category-status');
        if (sEl) sEl.value = '';
        if (stEl) stEl.value = '';
        if (btnEl) {
            btnEl.disabled = true;
            btnEl.innerHTML = '⏳ Đang tải...';
        }
    }
    const { ok, data } = await api('GET', '/api/categories');
    if (btnEl) {
        btnEl.disabled = false;
        btnEl.innerHTML = '🔄 Làm mới';
    }
    if (!ok || !Array.isArray(data)) return;
    appCategories = data;
    window.adminCategoriesCache = data;
    filterAdminCategories();
    if (isManualRefresh) {
        toast(`Đã làm mới danh sách (${data.length} danh mục) & đặt lại bộ lọc! 🔄`);
    }
}

function filterAdminCategories() {
    const all = window.adminCategoriesCache || appCategories || [];
    const q = (document.getElementById('admin-category-search')?.value || '').trim().toLowerCase();
    const status = document.getElementById('admin-category-status')?.value || '';

    const filtered = all.filter(c => {
        const isActive = c.isActive !== false;
        if (status === 'active' && !isActive) return false;
        if (status === 'inactive' && isActive) return false;
        if (q) {
            const mName = (c.name || '').toLowerCase().includes(q);
            const mCode = (c.code || '').toLowerCase().includes(q);
            const mSlug = (c.slug || '').toLowerCase().includes(q);
            const mDesc = (c.description || '').toLowerCase().includes(q);
            if (!mName && !mCode && !mSlug && !mDesc) return false;
        }
        return true;
    });

    const totalProds = filtered.reduce((acc, c) => acc + (c.productCount || 0), 0);
    const summaryEl = document.getElementById('admin-categories-summary');
    if (summaryEl) {
        summaryEl.innerHTML = `Hiển thị <b>${filtered.length}/${all.length}</b> danh mục • Tổng cộng <b>${totalProds}</b> sản phẩm đang liên kết`;
    }

    const tbody = document.getElementById('admin-categories-body');
    if (!tbody) return;

    if (!filtered.length) {
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center;padding:32px;color:var(--text-3)">Không tìm thấy danh mục phù hợp</td></tr>`;
        return;
    }

    tbody.innerHTML = filtered.map(c => {
        const templates = Array.isArray(c.specTemplates) && c.specTemplates.length
            ? c.specTemplates
            : getSpecTemplatesForCategory(c.id);
        const isActive = c.isActive !== false;
        return `
        <tr>
            <td>
                <img src="${c.imageUrl || '/images/placeholder.svg'}" onerror="this.onerror=null;this.src='/images/placeholder.svg';"
                     style="width:48px;height:48px;border-radius:10px;object-fit:cover;border:1px solid var(--border);background:#fff">
            </td>
            <td>
                <div style="font-weight:700;font-size:14px;color:var(--text)">${esc(c.name)}</div>
                <div style="display:flex;gap:6px;align-items:center;margin-top:4px;font-size:11.5px;color:var(--text-3)">
                    <span style="font-family:monospace;background:var(--bg-2);padding:1px 6px;border-radius:4px;border:1px solid var(--border);color:var(--primary);font-weight:600">${esc(c.code || 'CAT-' + c.id)}</span>
                    <span>•</span>
                    <span>/${esc(c.slug)}</span>
                </div>
            </td>
            <td>
                <div style="font-size:12.5px;color:var(--text-2);line-height:1.45;max-width:250px">${esc(c.description || 'Chưa có mô tả')}</div>
            </td>
            <td>
                <div style="display:flex;gap:4px;flex-wrap:wrap;max-width:300px">
                    ${templates.map(t => `<span style="font-size:11px;padding:2px 7px;border-radius:5px;background:var(--bg-2);border:1px solid var(--border);color:var(--text-2)">${esc(t)}</span>`).join('')}
                </div>
            </td>
            <td style="text-align:center">
                <span style="display:inline-flex;align-items:center;justify-content:center;min-width:34px;padding:3px 10px;border-radius:99px;font-weight:700;font-size:12.5px;background:#eef2ff;color:var(--primary)">
                    ${c.productCount || 0} SP
                </span>
            </td>
            <td>
                ${isActive
                    ? `<span class="badge badge-new">Đang hoạt động</span>`
                    : `<span class="badge" style="background:#f1f5f9;color:#64748b">Tạm ngưng</span>`}
            </td>
            <td>
                <div style="display:flex;gap:6px;align-items:center">
                    <button class="btn btn-outline btn-sm" onclick="editCategory(${c.id})" style="padding:5px 10px">✏️ Sửa</button>
                    <button class="btn btn-ghost btn-sm" style="color:var(--danger);padding:5px 8px" onclick="deleteCategory(${c.id})" title="Xóa danh mục">🗑️</button>
                </div>
            </td>
        </tr>`;
    }).join('');
}

function openNewCategoryModal() {
    document.getElementById('cat-id').value = '';
    document.getElementById('cat-name').value = '';
    document.getElementById('cat-code').value = 'Tự động sinh khi tạo';
    document.getElementById('cat-slug').value = '';
    document.getElementById('cat-active').value = 'true';
    document.getElementById('cat-image').value = '';
    document.getElementById('cat-desc').value = '';
    document.getElementById('cat-spec-templates').value = 'Thương hiệu\nLoại sản phẩm\nKết nối\nTính năng đặc biệt\nBảo hành';
    document.getElementById('cat-modal-title').textContent = '🗂️ Thêm Danh Mục Sản Phẩm Mới';
    document.getElementById('cat-submit-btn').textContent = '💾 Tạo Danh Mục';
    openModal('category-modal');
}

function editCategory(id) {
    const c = (window.adminCategoriesCache || appCategories || []).find(x => Number(x.id) === Number(id));
    if (!c) return;
    document.getElementById('cat-id').value = c.id;
    document.getElementById('cat-name').value = c.name || '';
    document.getElementById('cat-code').value = `#${c.id} • ${c.code || 'CAT-' + String(c.id).padStart(2, '0')}`;
    document.getElementById('cat-slug').value = c.slug || '';
    document.getElementById('cat-active').value = c.isActive === false ? 'false' : 'true';
    document.getElementById('cat-image').value = c.imageUrl || '';
    document.getElementById('cat-desc').value = c.description || '';
    const templates = Array.isArray(c.specTemplates) && c.specTemplates.length
        ? c.specTemplates
        : getSpecTemplatesForCategory(c.id);
    document.getElementById('cat-spec-templates').value = templates.join('\n');
    document.getElementById('cat-modal-title').textContent = `✏️ Chỉnh Sửa Danh Mục: ${c.name}`;
    document.getElementById('cat-submit-btn').textContent = '💾 Lưu Thay Đổi';
    openModal('category-modal');
}

async function submitSaveCategory(e) {
    e.preventDefault();
    const id = document.getElementById('cat-id').value;
    const btn = document.getElementById('cat-submit-btn');
    btn.disabled = true;
    btn.textContent = 'Đang lưu...';

    const specTemplates = (document.getElementById('cat-spec-templates').value || '')
        .split(/\r?\n/)
        .map(s => s.trim())
        .filter(Boolean);

    const body = {
        name: document.getElementById('cat-name').value.trim(),
        code: document.getElementById('cat-code').value.trim() || null,
        slug: document.getElementById('cat-slug').value.trim() || null,
        isActive: document.getElementById('cat-active').value !== 'false',
        imageUrl: document.getElementById('cat-image').value.trim() || null,
        description: document.getElementById('cat-desc').value.trim() || null,
        specTemplates
    };

    const res = id
        ? await api('PUT', `/api/categories/${id}`, body)
        : await api('POST', '/api/categories', body);

    btn.disabled = false;
    btn.textContent = id ? '💾 Lưu Thay Đổi' : '💾 Tạo Danh Mục';

    if (!res.ok) {
        return toast(res.data?.message || 'Lỗi khi lưu danh mục', 'error');
    }

    toast(id ? 'Đã cập nhật danh mục thành công! 🗂️' : 'Đã thêm danh mục mới thành công! 🗂️');
    closeModal('category-modal');
    await loadAdminCategories();
    await loadBrandsAndCategories();
}

async function deleteCategory(id) {
    const c = (window.adminCategoriesCache || appCategories || []).find(x => Number(x.id) === Number(id));
    if (c && c.productCount > 0) {
        return toast(`Không thể xóa danh mục "${c.name}" vì đang có ${c.productCount} sản phẩm thuộc danh mục này! Vui lòng chuyển sản phẩm sang danh mục khác hoặc chuyển trạng thái sang Tạm ngưng.`, 'error');
    }
    if (!confirm(`Bạn có chắc chắn muốn xóa danh mục "${c ? c.name : id}" không?`)) return;
    const { ok, data } = await api('DELETE', `/api/categories/${id}`);
    if (ok) {
        toast(data?.message || 'Đã xóa danh mục thành công! 🗑️');
        await loadAdminCategories();
        await loadBrandsAndCategories();
    } else {
        toast(data?.message || 'Không thể xóa danh mục này', 'error');
    }
}

async function loadAdminOrders(isManualRefresh = false, btnEl = null) {
    const filterEl = document.getElementById('admin-order-filter');
    if (isManualRefresh) {
        if (filterEl) filterEl.value = '';
        if (btnEl) {
            btnEl.disabled = true;
            btnEl.innerHTML = '⏳ Đang tải...';
        }
    }
    const status = filterEl?.value || '';
    let url = '/api/admin/orders';
    if (status) url += `?status=${status}`;
    const { ok, data } = await api('GET', url);
    if (btnEl) {
        btnEl.disabled = false;
        btnEl.innerHTML = '🔄 Làm mới';
    }
    if (!ok) return;
    if (isManualRefresh) {
        toast(`Đã làm mới danh sách (${data.length} đơn hàng)! 🔄`);
    }

    document.getElementById('admin-orders-body').innerHTML = data.map(o => `
        <tr>
            <td><span style="font-weight:700;color:var(--primary)">#${o.orderCode}</span></td>
            <td>
                <div style="font-size:13px;max-width:180px;line-height:1.4">${esc(o.shippingAddress)}</div>
            </td>
            <td>
                <div style="display:flex;flex-direction:column;gap:6px;max-width:280px">
                    ${o.items.map(it => `
                        <div style="font-size:12.5px;line-height:1.35;border-bottom:1px dashed var(--border);padding-bottom:4px">
                            <a href="/product/${it.productId}" target="_blank" style="text-decoration:none;font-weight:600;color:var(--text)" onmouseover="this.style.color='var(--primary)'" onmouseout="this.style.color='var(--text)'" title="Xem chi tiết sản phẩm">
                                ${esc(it.productName)}
                            </a>
                            <div style="display:flex;gap:6px;color:var(--text-3);font-size:11.5px;margin-top:2px;flex-wrap:wrap">
                                <span style="font-weight:700;color:var(--primary)">x${it.quantity}</span>
                                ${it.variant ? `<span>• Bản: <b>${esc(it.variant)}</b></span>` : ''}
                                ${it.color ? `<span>• Màu: <b>${esc(it.color)}</b></span>` : ''}
                            </div>
                        </div>
                    `).join('')}
                </div>
            </td>
            <td style="font-weight:700">${fmt(o.totalAmount)}</td>
            <td>
                <span style="color:${o.paymentStatus==='paid'?'var(--success)':'var(--danger)'};font-weight:600">
                    ${o.paymentStatus === 'paid' ? '✓ Đã TT' : '✗ Chưa TT'}
                </span>
                ${o.paymentStatus !== 'paid' ? `
                    <button class="btn btn-sm btn-primary" style="margin-left:6px;padding:3px 8px;font-size:11px" onclick="adminConfirmPayment(${o.id})" title="Xác nhận tiền đã vào tài khoản">
                        ✓ Duyệt TT
                    </button>
                ` : ''}
            </td>
            <td>
                <select class="admin-status-select status-${o.status}" onchange="updateOrderStatus(${o.id}, this.value)">
                    <option value="pending"    ${o.status==='pending'?'selected':''}>⏳ Chờ xử lý</option>
                    <option value="processing" ${o.status==='processing'?'selected':''}>⚙️ Đang xử lý</option>
                    <option value="shipped"    ${o.status==='shipped'?'selected':''}>🚚 Đang giao</option>
                    <option value="delivered"  ${o.status==='delivered'?'selected':''}>✅ Đã giao</option>
                    <option value="cancelled"  ${o.status==='cancelled'?'selected':''}>❌ Đã hủy</option>
                </select>
            </td>
            <td style="color:var(--text-3);font-size:13px">${new Date(o.orderedAt).toLocaleDateString('vi-VN')}</td>
        </tr>`).join('');
}

async function adminConfirmPayment(orderId) {
    if (!confirm('Bạn đã kiểm tra sao kê MB Bank (1220820056666) và xác nhận tiền đã vào tài khoản?')) return;
    const { ok, data } = await api('PUT', `/api/admin/orders/${orderId}/confirm-payment`);
    if (ok) {
        toast('Đã xác nhận thanh toán thành công! ✓');
        loadAdminOrders();
        if (typeof loadWarehouseOrders === 'function') loadWarehouseOrders();
    } else {
        toast(data?.message || 'Lỗi khi xác nhận thanh toán', 'error');
    }
}

async function updateOrderStatus(id, status) {
    const { ok } = await api('PUT', `/api/admin/orders/${id}/status`, { status });
    if (!ok) toast('Cập nhật thất bại', 'error');
    else toast('Đã cập nhật trạng thái ✓');
}

let adminUserSearchTimer;
function debouncedLoadAdminUsers() {
    clearTimeout(adminUserSearchTimer);
    adminUserSearchTimer = setTimeout(() => {
        loadAdminUsers();
    }, 350);
}

async function loadAdminUsers(isManualRefresh = false, btnEl = null) {
    if (isManualRefresh) {
        const sEl = document.getElementById('admin-user-search');
        const rEl = document.getElementById('admin-user-role-filter');
        if (sEl) sEl.value = '';
        if (rEl) rEl.value = '';
        if (btnEl) {
            btnEl.disabled = true;
            btnEl.innerHTML = '⏳ Đang tải...';
        }
    }
    const search = document.getElementById('admin-user-search')?.value.trim() || '';
    const role = document.getElementById('admin-user-role-filter')?.value || '';
    let url = '/api/admin/users?';
    if (search) url += `search=${encodeURIComponent(search)}&`;
    if (role) url += `role=${encodeURIComponent(role)}&`;

    const { ok, data } = await api('GET', url);
    if (btnEl) {
        btnEl.disabled = false;
        btnEl.innerHTML = '🔄 Làm mới';
    }
    const tbody = document.getElementById('admin-users-body');
    if (!ok || !Array.isArray(data)) {
        if (tbody) tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;color:var(--danger);padding:24px">Không thể tải danh sách tài khoản</td></tr>';
        return;
    }
    if (isManualRefresh) {
        toast(`Đã làm mới danh sách (${data.length} người dùng) & đặt lại bộ lọc! 🔄`);
    }
    if (data.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;color:var(--text-3);padding:24px">Không tìm thấy tài khoản nào</td></tr>';
        return;
    }
    tbody.innerHTML = data.map((u, i) => `
        <tr>
            <td style="color:var(--text-3)">${i+1}</td>
            <td style="font-weight:700">${esc(u.username)}</td>
            <td>${esc(u.fullName)}</td>
            <td>
                <div>${esc(u.email)}</div>
                <div style="font-size:12px;color:var(--text-3)">${esc(u.phone) || '–'}</div>
            </td>
            <td>
                <select class="admin-status-select" onchange="updateUserRole(${u.id}, this.value)" style="padding:4px 8px;font-size:12px;border:1.5px solid var(--border);border-radius:6px;font-weight:600">
                    <option value="customer" ${u.role==='customer'?'selected':''}>👤 Khách hàng</option>
                    <option value="support" ${u.role==='support'?'selected':''}>🎧 CSKH / Bán hàng</option>
                    <option value="warehouse" ${u.role==='warehouse'?'selected':''}>📦 Thủ kho</option>
                    <option value="admin" ${u.role==='admin'?'selected':''}>👑 Quản trị viên</option>
                </select>
            </td>
            <td>
                <span class="badge" style="${u.isLocked ? 'background:#fee2e2;color:#991b1b;border:1px solid #fca5a5' : 'background:#dcfce7;color:#166534;border:1px solid #86efac'}">
                    ${u.isLocked ? '🔒 Đã khóa' : '✓ Hoạt động'}
                </span>
            </td>
            <td>
                <button class="btn btn-sm ${u.isLocked ? 'btn-primary' : 'btn-outline'}" 
                        style="padding:4px 10px;font-size:12px;${u.isLocked ? '' : 'color:var(--danger);border-color:#fca5a5'}"
                        onclick="toggleUserLock(${u.id}, ${!u.isLocked})">
                    ${u.isLocked ? '🔓 Mở khóa' : '🔒 Khóa'}
                </button>
            </td>
        </tr>`).join('');
}

async function updateUserRole(id, role) {
    const { ok, data } = await api('PUT', `/api/admin/users/${id}/role`, { role });
    if (ok) {
        toast('Cập nhật quyền thành công! ✓');
        loadAdminUsers();
    } else {
        toast(data?.message || 'Lỗi khi cập nhật quyền', 'error');
        loadAdminUsers();
    }
}

async function toggleUserLock(id, isLocked) {
    const confirmMsg = isLocked 
        ? 'Bạn có chắc muốn KHÓA tài khoản này không? Người dùng sẽ không thể đăng nhập.'
        : 'Bạn có chắc muốn MỞ KHÓA tài khoản này không?';
    if (!confirm(confirmMsg)) return;

    const { ok, data } = await api('PUT', `/api/admin/users/${id}/lock`, { isLocked });
    if (ok) {
        toast(isLocked ? 'Đã khóa tài khoản 🔒' : 'Đã mở khóa tài khoản 🔓');
        loadAdminUsers();
    } else {
        toast(data?.message || 'Lỗi khi thay đổi trạng thái khóa', 'error');
    }
}

/* WAREHOUSE LOGIC */
function showWarehouseTab(tab, el) {
    ['inventory','orders'].forEach(t => {
        const pane = document.getElementById(`warehouse-tab-${t}`);
        if(pane) pane.style.display = 'none';
    });
    const target = document.getElementById(`warehouse-tab-${tab}`);
    if(target) target.style.display = 'block';

    const items = el.parentElement.querySelectorAll('.admin-nav-item');
    items.forEach(i => i.classList.remove('active'));
    el.classList.add('active');

    if(tab === 'inventory') loadWarehouseInventory();
    if(tab === 'orders') loadWarehouseOrders();
}

async function loadWarehouseInventory() {
    const { ok, data } = await api('GET', '/api/products');
    if (!ok || !Array.isArray(data)) return;

    document.getElementById('warehouse-inventory-body').innerHTML = data.map(p => {
        const variants = Array.isArray(p.variants) ? p.variants : [];
        return `
        <tr>
            <td><img src="${p.primaryImage || '/images/placeholder.svg'}" onerror="this.onerror=null;this.src='/images/placeholder.svg';" style="width:46px;height:46px;border-radius:8px;object-fit:cover;border:1px solid var(--border)"></td>
            <td>
                <div style="font-weight:700;color:var(--text)">${esc(p.name)}</div>
                <div style="font-size:11.5px;color:var(--text-3)">#${p.id} • ${esc(p.brandName || '')}</div>
            </td>
            <td>
                <div style="display:flex;gap:5px;flex-wrap:wrap;max-width:440px">
                    ${variants.map(v => `
                        <span style="display:inline-flex;align-items:center;gap:4px;font-size:11px;padding:3px 8px;border-radius:6px;background:var(--bg-2);border:1px solid var(--border)">
                            <span style="width:8px;height:8px;border-radius:50%;background:${v.colorCode || '#64748b'};display:inline-block"></span>
                            <span>${esc(v.variantName)} - ${esc(v.colorName)}:</span>
                            <b style="color:${v.stockQuantity > 0 ? 'var(--primary)' : 'var(--danger)'}">${v.stockQuantity}</b>
                        </span>
                    `).join('')}
                </div>
            </td>
            <td><span class="badge ${p.stockQuantity>0?'badge-new':'badge-hot'}">${p.stockQuantity}</span></td>
            <td>
                <div style="display:flex;gap:6px;align-items:center;flex-wrap:wrap">
                    <select id="wh-variant-${p.id}" style="padding:6px 8px;border:1px solid var(--border);border-radius:var(--radius-sm);font-size:12px;max-width:165px">
                        ${variants.map(v => `<option value="${v.id}">${esc(v.variantName)} - ${esc(v.colorName)} (${v.stockQuantity})</option>`).join('')}
                    </select>
                    <input type="number" id="wh-stock-${p.id}" style="width:70px;padding:6px;border:1px solid var(--border);border-radius:var(--radius-sm);font-size:12.5px" value="5" placeholder="+/-">
                    <button class="btn btn-primary btn-sm" onclick="addStock(${p.id})">Cập nhật</button>
                </div>
            </td>
        </tr>`;
    }).join('');
}

async function addStock(id) {
    const val = parseInt(document.getElementById(`wh-stock-${id}`)?.value, 10);
    if (!val || val === 0) return toast('Vui lòng nhập số lượng > 0 (nhập kho) hoặc < 0 (xuất kho)', 'error');
    const variantSelect = document.getElementById(`wh-variant-${id}`);
    const variantId = variantSelect && variantSelect.value ? parseInt(variantSelect.value, 10) : null;

    const { ok } = await api('PATCH', `/api/products/${id}/stock`, { stockQuantity: val, variantId });
    if (ok) {
        toast('Đã cập nhật tồn kho biến thể & đồng bộ hệ thống! 📦');
        loadWarehouseInventory();
    } else {
        toast('Lỗi cập nhật kho', 'error');
    }
}

async function loadWarehouseOrders() {
    const status = document.getElementById('warehouse-order-filter').value;
    let url = '/api/admin/orders';
    if (status) url += `?status=${status}`;
    const { ok, data } = await api('GET', url);
    if (!ok) return;

    document.getElementById('warehouse-orders-body').innerHTML = data.map(o => `
        <tr>
            <td><span style="font-weight:700;color:var(--primary)">#${o.orderCode}</span></td>
            <td>${o.shippingAddress?.split(',').slice(-1)[0]?.trim() || '–'}</td>
            <td>${o.items.length} SP</td>
            <td style="font-weight:700">${fmt(o.totalAmount)}</td>
            <td>
                <span style="color:${o.paymentStatus==='paid'?'var(--success)':'var(--danger)'};font-weight:600">
                    ${o.paymentStatus === 'paid' ? '✓ TT' : '✗ Chưa TT'}
                </span>
                ${o.paymentStatus !== 'paid' ? `
                    <button class="btn btn-sm btn-primary" style="margin-left:6px;padding:2px 6px;font-size:11px" onclick="adminConfirmPayment(${o.id})" title="Xác nhận tiền đã vào tài khoản">
                        ✓ Duyệt TT
                    </button>
                ` : ''}
            </td>
            <td>
                <select class="admin-status-select status-${o.status}" onchange="updateWarehouseOrderStatus(${o.id}, this.value)">
                    <option value="pending"    ${o.status==='pending'?'selected':''} disabled>⏳ Chờ xử lý</option>
                    <option value="processing" ${o.status==='processing'?'selected':''}>⚙️ Đang xử lý</option>
                    <option value="shipped"    ${o.status==='shipped'?'selected':''}>🚚 Đang giao</option>
                    <option value="delivered"  ${o.status==='delivered'?'selected':''}>✅ Đã giao</option>
                    <option value="cancelled"  ${o.status==='cancelled'?'selected':''} disabled>❌ Đã hủy</option>
                </select>
            </td>
            <td style="color:var(--text-3);font-size:13px">${new Date(o.orderedAt).toLocaleDateString('vi-VN')}</td>
        </tr>`).join('');
}

async function updateWarehouseOrderStatus(id, status) {
    const { ok } = await api('PUT', `/api/admin/orders/${id}/status`, { status });
    if (!ok) toast('Cập nhật thất bại', 'error');
    else toast('Đã cập nhật trạng thái ✓');
}

/* SEARCH (debounced) */
let searchTimer;
document.getElementById('search-input').addEventListener('input', () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => { if (currentPage === 'home') loadProducts(); }, 400);
});
document.getElementById('search-input').addEventListener('keydown', e => {
    if (e.key === 'Enter') doSearch();
});

/* LIVE CHAT & AI CHATBOT SYSTEM (GỘP CHUNG AI & CSKH) */
let chatSessionId = getEffectiveChatSessionId();
localStorage.setItem('techstore_chat_session', chatSessionId);
let chatPollingTimer = null;
let isChatOpen = false;
let chatJustDragged = false;

function onChatTriggerClick(e) {
    if (chatJustDragged) return;
    if (!isLoggedIn()) {
        toast('Vui lòng đăng nhập để sử dụng tính năng trò chuyện!', 'info');
        openAuthModal();
        return;
    }
    toggleFloatingChat();
}

function adjustChatBoxPosition() {
    const box = document.getElementById('floating-chat-box');
    const container = document.getElementById('floating-chat-container');
    if (!box || !container) return;
    
    // Check if user already moved the chat box to a custom position
    try {
        const savedBoxPos = localStorage.getItem('techstore_chat_box_pos');
        if (savedBoxPos) {
            const { left, top } = JSON.parse(savedBoxPos);
            const boxW = Math.min(390, window.innerWidth - 20);
            const boxH = Math.min(580, window.innerHeight - 40);
            box.style.width = boxW + 'px';
            box.style.height = boxH + 'px';
            const maxL = Math.max(10, window.innerWidth - boxW - 10);
            const maxT = Math.max(10, window.innerHeight - boxH - 10);
            box.style.left = Math.min(Math.max(10, left), maxL) + 'px';
            box.style.top = Math.min(Math.max(10, top), maxT) + 'px';
            box.style.right = 'auto';
            box.style.bottom = 'auto';
            return;
        }
    } catch(e) {}

    // Default placement near trigger button
    const cRect = container.getBoundingClientRect();
    const availW = Math.min(390, window.innerWidth - 24);
    const availH = Math.min(580, window.innerHeight - 80);
    box.style.width = availW + 'px';
    box.style.height = availH + 'px';

    // X alignment
    let idealLeft = cRect.right - availW;
    if (idealLeft < 10) idealLeft = cRect.left;
    const targetX = Math.min(Math.max(10, idealLeft), window.innerWidth - availW - 10);
    box.style.left = targetX + 'px';
    box.style.right = 'auto';

    // Y alignment
    let idealTop = cRect.top - availH - 12;
    if (idealTop < 10) idealTop = cRect.bottom + 12;
    const targetY = Math.min(Math.max(10, idealTop), window.innerHeight - availH - 10);
    box.style.top = targetY + 'px';
    box.style.bottom = 'auto';
}

function initDraggableChatBox() {
    const box = document.getElementById('floating-chat-box');
    const header = document.querySelector('.chat-box-header');
    if (!box || !header) return;

    let isDragging = false;
    let startX = 0, startY = 0;
    let initialLeft = 0, initialTop = 0;

    header.addEventListener('pointerdown', (e) => {
        if (e.target.closest('.chat-box-close') || (e.button && e.button !== 0)) return;

        isDragging = true;
        startX = e.clientX;
        startY = e.clientY;

        const rect = box.getBoundingClientRect();
        initialLeft = rect.left;
        initialTop = rect.top;

        header.classList.add('dragging');
        try {
            header.setPointerCapture(e.pointerId);
        } catch(err) {}
        e.preventDefault();
    });

    header.addEventListener('pointermove', (e) => {
        if (!isDragging) return;

        const dx = e.clientX - startX;
        const dy = e.clientY - startY;

        const boxW = box.offsetWidth;
        const boxH = box.offsetHeight;
        const maxL = Math.max(10, window.innerWidth - boxW - 10);
        const maxT = Math.max(10, window.innerHeight - boxH - 10);

        const newL = Math.min(Math.max(10, initialLeft + dx), maxL);
        const newT = Math.min(Math.max(10, initialTop + dy), maxT);

        box.style.left = newL + 'px';
        box.style.top = newT + 'px';
        box.style.right = 'auto';
        box.style.bottom = 'auto';
    });

    const onPointerEnd = (e) => {
        if (!isDragging) return;
        isDragging = false;
        header.classList.remove('dragging');

        try {
            header.releasePointerCapture(e.pointerId);
        } catch(err) {}

        const rect = box.getBoundingClientRect();
        try {
            localStorage.setItem('techstore_chat_box_pos', JSON.stringify({
                left: Math.round(rect.left),
                top: Math.round(rect.top)
            }));
        } catch(err) {}
    };

    header.addEventListener('pointerup', onPointerEnd);
    header.addEventListener('pointercancel', onPointerEnd);
}

function initDraggableChat() {
    initDraggableChatBox();
    const container = document.getElementById('floating-chat-container');
    const trigger = document.getElementById('floating-chat-trigger');
    if (!container || !trigger) return;

    let isPointerDown = false;
    let hasDragged = false;
    let startX = 0, startY = 0;
    let initialLeft = 0, initialTop = 0;

    // Restore saved position if available
    try {
        const savedPos = localStorage.getItem('techstore_chat_btn_pos');
        if (savedPos) {
            const { left, top } = JSON.parse(savedPos);
            const btnSize = 62;
            const maxL = Math.max(10, window.innerWidth - btnSize - 10);
            const maxT = Math.max(10, window.innerHeight - btnSize - 10);
            const clampL = Math.min(Math.max(10, left), maxL);
            const clampT = Math.min(Math.max(10, top), maxT);
            container.style.left = clampL + 'px';
            container.style.top = clampT + 'px';
            container.style.right = 'auto';
            container.style.bottom = 'auto';
            if (clampL < 120) trigger.classList.add('tooltip-right');
            else trigger.classList.remove('tooltip-right');
        }
    } catch(e) {}

    trigger.addEventListener('pointerdown', (e) => {
        if (e.button && e.button !== 0) return;
        isPointerDown = true;
        hasDragged = false;
        startX = e.clientX;
        startY = e.clientY;

        const rect = container.getBoundingClientRect();
        initialLeft = rect.left;
        initialTop = rect.top;

        try {
            trigger.setPointerCapture(e.pointerId);
        } catch(err) {}
    });

    trigger.addEventListener('pointermove', (e) => {
        if (!isPointerDown) return;
        const dx = e.clientX - startX;
        const dy = e.clientY - startY;

        if (!hasDragged && Math.hypot(dx, dy) > 5) {
            hasDragged = true;
            trigger.classList.add('dragging');
        }

        if (hasDragged) {
            const btnSize = 62;
            const maxL = Math.max(10, window.innerWidth - btnSize - 10);
            const maxT = Math.max(10, window.innerHeight - btnSize - 10);
            const newL = Math.min(Math.max(10, initialLeft + dx), maxL);
            const newT = Math.min(Math.max(10, initialTop + dy), maxT);

            container.style.left = newL + 'px';
            container.style.top = newT + 'px';
            container.style.right = 'auto';
            container.style.bottom = 'auto';
            if (newL < 120) trigger.classList.add('tooltip-right');
            else trigger.classList.remove('tooltip-right');

            if (isChatOpen) {
                adjustChatBoxPosition();
            }
        }
    });

    const onPointerEnd = (e) => {
        if (!isPointerDown) return;
        isPointerDown = false;
        trigger.classList.remove('dragging');

        try {
            trigger.releasePointerCapture(e.pointerId);
        } catch(err) {}

        if (hasDragged) {
            chatJustDragged = true;
            const rect = container.getBoundingClientRect();
            try {
                localStorage.setItem('techstore_chat_btn_pos', JSON.stringify({
                    left: Math.round(rect.left),
                    top: Math.round(rect.top)
                }));
            } catch(e) {}
            setTimeout(() => { chatJustDragged = false; }, 200);
        }
    };

    trigger.addEventListener('pointerup', onPointerEnd);
    trigger.addEventListener('pointercancel', onPointerEnd);

    window.addEventListener('resize', () => {
        if (container.style.left) {
            const btnSize = 62;
            const rect = container.getBoundingClientRect();
            const maxL = Math.max(10, window.innerWidth - btnSize - 10);
            const maxT = Math.max(10, window.innerHeight - btnSize - 10);
            const newL = Math.min(Math.max(10, rect.left), maxL);
            const newT = Math.min(Math.max(10, rect.top), maxT);
            container.style.left = newL + 'px';
            container.style.top = newT + 'px';
        }
        if (isChatOpen) adjustChatBoxPosition();
    });
}

function toggleFloatingChat() {
    if (!isLoggedIn()) {
        toast('Vui lòng đăng nhập để sử dụng tính năng trò chuyện!', 'info');
        openAuthModal();
        return;
    }
    const box = document.getElementById('floating-chat-box');
    isChatOpen = !isChatOpen;
    box.style.display = isChatOpen ? 'flex' : 'none';
    if (isChatOpen) {
        document.getElementById('chat-unread-badge').style.display = 'none';
        adjustChatBoxPosition();
        loadChatHistory(true); // Khi mở chat thì cuộn xuống đáy
        startChatPolling();
    } else {
        stopChatPolling();
    }
}

function startChatPolling() {
    stopChatPolling();
    // Khi polling chạy ngầm định kỳ: KHÔNG ép cuộn xuống đáy (forceScroll = false)
    chatPollingTimer = setInterval(() => loadChatHistory(false), 3500);
}

function stopChatPolling() {
    if (chatPollingTimer) {
        clearInterval(chatPollingTimer);
        chatPollingTimer = null;
    }
}

async function loadChatHistory(forceScroll = false) {
    const { ok, data } = await api('GET', `/api/chat/history?sessionId=${encodeURIComponent(chatSessionId)}`);
    if (!ok || !data?.history) return;
    renderChatMessages(data.history, forceScroll);
}

/**
 * Cập nhật chat widget theo role của người dùng hiện tại:
 * - customer / guest: widget tư vấn bán hàng bình thường
 * - admin: widget trợ lý quản trị nội bộ
 * - warehouse: widget trợ lý kho hàng
 * - support: ẩn hoàn toàn widget (nhân viên CSKH dùng Desk)
 */
function updateChatWidgetForRole() {
    const role = authUser?.role || 'customer';
    const container = document.getElementById('floating-chat-container');
    if (!container) return;

    // Nhân viên CSKH không dùng chatbot
    if (role === 'support') {
        container.style.display = 'none';
        return;
    }
    container.style.display = '';

    const titleEl = container.querySelector('.chat-box-header [style*="font-weight:700"]');
    const subtitleEl = container.querySelector('.chat-box-header [style*="font-size:11.5px"]');
    const bannerEl = document.getElementById('chat-mode-banner');
    const inputEl = document.getElementById('chat-user-input');
    const quickChips = document.getElementById('chat-quick-chips');
    const iconEl = container.querySelector('.chat-box-header [style*="border-radius:50%"]');

    if (role === 'admin') {
        if (titleEl) titleEl.childNodes[0].textContent = 'Trợ lý Quản trị ';
        if (subtitleEl) subtitleEl.textContent = 'Hỗ trợ nội bộ • Admin TechStore';
        if (iconEl) iconEl.textContent = '🛠️';
        if (bannerEl) bannerEl.innerHTML = '<span style="font-size:14px">🛠️</span><span>Bot quản trị nội bộ — hỏi về <strong>đơn hàng, người dùng, sản phẩm</strong>. Không dành cho khách hàng.</span>';
        if (inputEl) inputEl.placeholder = 'Hỏi về quản trị, đơn hàng, thống kê...';
        if (quickChips) quickChips.style.display = 'none';
    } else if (role === 'warehouse') {
        if (titleEl) titleEl.childNodes[0].textContent = 'Trợ lý Kho hàng ';
        if (subtitleEl) subtitleEl.textContent = 'Hỗ trợ nội bộ • Thủ kho TechStore';
        if (iconEl) iconEl.textContent = '📦';
        if (bannerEl) bannerEl.innerHTML = '<span style="font-size:14px">📦</span><span>Bot kho hàng nội bộ — hỏi về <strong>tồn kho, nhập xuất</strong>. Không dành cho khách hàng.</span>';
        if (inputEl) inputEl.placeholder = 'Hỏi về tồn kho, nhập xuất hàng...';
        if (quickChips) quickChips.style.display = 'none';
    } else {
        // customer / guest — restore default
        if (titleEl) titleEl.childNodes[0].textContent = 'Hỗ Trợ Khách Hàng ';
        if (subtitleEl) subtitleEl.textContent = 'Trực tuyến • AI & Nhân viên tư vấn';
        if (iconEl) iconEl.textContent = '💬';
        if (bannerEl) bannerEl.innerHTML = '<span style="font-size:14px">💡</span><span>Bấm câu hỏi gợi ý để <strong>Trợ lý AI</strong> giải đáp nhanh, hoặc nhập tin nhắn để gửi <strong>Nhân viên CSKH</strong>!</span>';
        if (inputEl) inputEl.placeholder = 'Nhắn tin với nhân viên tư vấn...';
        if (quickChips) quickChips.style.display = 'flex';
    }
}

function renderChatMessages(messages, forceScroll = false) {
    const body = document.getElementById('chat-messages-body');
    if (!body) return; // Bảo vệ nếu đang ở trang không có chat widget (như CSKH Desk)

    if (!messages || !messages.length) {
        body.dataset.renderedKey = '';
        const role = authUser?.role || 'customer';
        let emptyGreeting = 'Xin chào! TechStore có thể giúp gì cho bạn?';
        let emptySub = 'Bấm các câu hỏi gợi ý bên dưới để <strong>Trợ lý AI giải đáp nhanh</strong>,<br>hoặc gõ tin nhắn để <strong>chat trực tiếp với Nhân viên CSKH</strong>!';
        let emptyIcon = '👋';

        if (role === 'admin') {
            emptyIcon = '🛠️';
            emptyGreeting = 'Trợ lý AI Quản trị TechStore';
            emptySub = 'Tôi là trợ lý AI nội bộ dành riêng cho Quản trị viên.<br>Hãy đặt câu hỏi về <strong>đơn hàng, người dùng, sản phẩm hoặc doanh thu</strong>!';
        } else if (role === 'warehouse') {
            emptyIcon = '📦';
            emptyGreeting = 'Trợ lý AI Kho hàng TechStore';
            emptySub = 'Tôi là trợ lý AI nội bộ dành riêng cho Thủ kho.<br>Hãy hỏi về <strong>tồn kho, cảnh báo hàng sắp hết, nhập xuất kho</strong>!';
        }

        body.innerHTML = `
            <div style="text-align:center;padding:32px 14px;color:var(--text-3)">
                <div style="width:52px;height:52px;border-radius:50%;background:#eef2ff;color:#4f46e5;display:flex;align-items:center;justify-content:center;font-size:26px;margin:0 auto 12px;box-shadow:0 2px 8px rgba(79,70,229,.15)">${emptyIcon}</div>
                <div style="font-weight:700;font-size:15px;color:var(--text);margin-bottom:6px">${emptyGreeting}</div>
                <div style="font-size:12.5px;line-height:1.55;color:var(--text-2);max-width:300px;margin:0 auto">
                    ${emptySub}
                </div>
            </div>`;
        return;
    }

    // 1. Kiểm tra vị trí cuộn: Người dùng có đang cuộn lên để đọc tin nhắn cũ hay không?
    // Nếu khoảng cách tới đáy >= 60px nghĩa là người dùng ĐANG ĐỌC TIN NHẮN CŨ PHÍA TRÊN
    const wasNearBottom = (body.scrollHeight - body.scrollTop - body.clientHeight) < 60;

    // 2. Kiểm tra nếu danh sách tin nhắn không có gì thay đổi thì KHÔNG re-render để tránh giật lag và mất vị trí cuộn
    const newKey = messages.length + '_' + (messages[messages.length - 1]?.id || 0) + '_' + (messages[messages.length - 1]?.message?.length || 0);
    if (!forceScroll && body.dataset.renderedKey === newKey) {
        return;
    }
    body.dataset.renderedKey = newKey;

    // 3. Render danh sách tin nhắn
    body.innerHTML = messages.map(m => {
        const isCustomer = m.sender === 'customer';
        const isStaff = m.sender === 'staff';
        const isBot = m.sender === 'bot';

        const senderCls = isCustomer ? 'customer' : (isStaff ? 'staff' : 'bot');
        const timeStr = m.createdAt ? new Date(m.createdAt).toLocaleTimeString('vi-VN', {hour:'2-digit', minute:'2-digit'}) : '';

        // Hiển thị thông báo chuyển giao khách hàng (nếu có)
        if (m.message && m.message.includes('🔄')) {
            return `
                <div style="text-align:center;margin:6px 0;">
                    <div style="display:inline-block;padding:5px 12px;background:#f1f5f9;color:#475569;border-radius:16px;font-size:11.5px;border:1px dashed #cbd5e1;line-height:1.4">
                        ${esc(m.message)}
                    </div>
                </div>`;
        }

        // Định danh vai trò hiển thị:
        let displayName = 'Bạn';
        let badgeIcon = '👤';
        if (isStaff) {
            const cleanName = formatStaffDisplayName(m.senderName);
            displayName = cleanName ? `CSKH (${cleanName})` : 'Nhân viên CSKH';
            badgeIcon = '🎧';
        } else if (isBot) {
            displayName = m.senderName || 'Trợ lý AI TechStore';
            badgeIcon = '🤖';
        }

        return `
            <div class="chat-bubble-row ${senderCls}">
                <div class="chat-bubble-meta">
                    <span class="chat-avatar-pill">${badgeIcon} ${esc(displayName)}</span>
                    <span>${timeStr}</span>
                </div>
                <div class="chat-bubble">${formatChatMessage(m.message)}</div>
            </div>`;
    }).join('');

    // 4. Chỉ tự động cuộn xuống đáy khi:
    // - Vừa mở chat / người dùng vừa gửi tin (forceScroll = true)
    // - Người dùng đang đứng ở sát đáy (wasNearBottom = true)
    // NẾU NGƯỜI DÙNG ĐANG CUỘN LÊN ĐỌC TIN CŨ -> GIỮ NGUYÊN, KHÔNG TỰ ĐỘNG CUỘN XUỐNG!
    if (forceScroll || wasNearBottom) {
        body.scrollTop = body.scrollHeight;
    }
}

function resetCustomerChatSession() {
    if (!isLoggedIn()) {
        toast('Vui lòng đăng nhập để sử dụng chat', 'info');
        openAuthModal();
        return;
    }
    loadChatHistory();
    toast('Đã làm mới cuộc hội thoại!', 'success');
}

function formatChatMessage(text) {
    if (!text) return '';
    return esc(text)
        .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
        .replace(/\*(.*?)\*/g, '<em>$1</em>')
        .replace(/`([^`]+)`/g, '<code style="background:rgba(0,0,0,.08);padding:1px 4px;border-radius:4px">$1</code>')
        .replace(/\n/g, '<br>');
}

// Khi người dùng bấm vào câu hỏi mẫu: Trợ lý AI sẽ trả lời câu hỏi này
async function sendAiQuickPrompt(promptText) {
    if (!isLoggedIn()) {
        toast('Vui lòng đăng nhập để sử dụng tính năng trò chuyện!', 'info');
        openAuthModal();
        return;
    }

    const body = document.getElementById('chat-messages-body');

    // 1. Hiển thị tin nhắn câu hỏi của khách hàng
    const tempCustomerRow = document.createElement('div');
    tempCustomerRow.className = 'chat-bubble-row customer';
    tempCustomerRow.innerHTML = `
        <div class="chat-bubble-meta">
            <span>👤 Bạn</span>
            <span>Vừa xong</span>
        </div>
        <div class="chat-bubble">${esc(promptText)}</div>`;
    body.appendChild(tempCustomerRow);

    // 2. Hiển thị bong bóng chờ AI suy nghĩ
    const typingRow = document.createElement('div');
    typingRow.className = 'chat-bubble-row bot';
    typingRow.id = 'chat-typing-row';
    typingRow.innerHTML = `
        <div class="chat-bubble-meta"><span>🤖 Trợ lý AI TechStore</span></div>
        <div class="chat-bubble" style="font-style:italic;color:var(--text-3)">Đang tra cứu dữ liệu & suy nghĩ...</div>`;
    body.appendChild(typingRow);
    body.scrollTop = body.scrollHeight;

    // 3. Gửi câu hỏi tới AI Backend
    const { ok, data } = await api('POST', '/api/chat/send', {
        sessionId: chatSessionId,
        message: promptText,
        target: 'bot'
    });

    typingRow.remove();

    if (ok) {
        await loadChatHistory(true); // Cuộn xuống đáy để xem câu trả lời của AI
    } else {
        toast('Lỗi khi tra cứu với AI', 'error');
    }
}

// Khi người dùng gõ tin nhắn vào ô input
async function handleSendChatMessage(e) {
    e.preventDefault();
    if (!isLoggedIn()) {
        toast('Vui lòng đăng nhập để sử dụng tính năng trò chuyện!', 'info');
        openAuthModal();
        return;
    }

    const input = document.getElementById('chat-user-input');
    const msg = input.value.trim();
    if (!msg) return;

    input.value = '';
    const body = document.getElementById('chat-messages-body');

    // 1. Hiển thị tin nhắn người dùng
    const tempCustomerRow = document.createElement('div');
    tempCustomerRow.className = 'chat-bubble-row customer';
    tempCustomerRow.innerHTML = `
        <div class="chat-bubble-meta">
            <span>👤 Bạn</span>
            <span>Vừa xong</span>
        </div>
        <div class="chat-bubble">${esc(msg)}</div>`;
    body.appendChild(tempCustomerRow);

    const role = authUser?.role || 'customer';
    const isInternal = (role === 'admin' || role === 'warehouse' || role === 'support');

    // 2. Với actor nội bộ (admin, warehouse): CHỈ CÓ AI TRẢ LỜI, hiện bong bóng chờ AI
    let typingRow = null;
    if (isInternal) {
        typingRow = document.createElement('div');
        typingRow.className = 'chat-bubble-row bot';
        let typingBotName = 'Trợ lý AI TechStore';
        if (role === 'admin') typingBotName = '🛠️ Trợ lý Quản trị TechStore';
        else if (role === 'warehouse') typingBotName = '📦 Trợ lý Kho hàng TechStore';

        typingRow.innerHTML = `
            <div class="chat-bubble-meta"><span>${typingBotName}</span></div>
            <div class="chat-bubble" style="font-style:italic;color:var(--text-3)">Đang phân tích & trả lời...</div>`;
        body.appendChild(typingRow);
    }
    body.scrollTop = body.scrollHeight;

    // 3. Gửi tới Backend:
    // - Actor nội bộ: Luôn gửi target = 'bot'
    // - Khách hàng: Gửi target = 'staff'
    const targetMode = isInternal ? 'bot' : 'staff';
    const { ok, data } = await api('POST', '/api/chat/send', {
        sessionId: chatSessionId,
        message: msg,
        target: targetMode
    });

    if (typingRow) typingRow.remove();

    if (ok) {
        if (!isInternal && targetMode === 'staff') {
            toast('Đã gửi tin nhắn đến nhân viên CSKH! 🎧', 'info');
        }
        await loadChatHistory(true); // Cuộn xuống đáy để xem phản hồi
    } else {
        toast('Lỗi khi gửi tin nhắn', 'error');
    }
}

/* CSKH SUPPORT DESK (STAFF WORKSPACE) */
let cskhActiveSessionId = null;
let cskhConversations = [];
let cskhDeskPollingTimer = null;
let cskhFilter = 'waiting'; // 'waiting' | 'my'
let activeCskhConversation = null;

async function loadSupportDesk() {
    stopCskhDeskPolling();
    setCskhFilter('waiting'); // Default tab là Chờ nhận
    await loadSupportConversations();
    cskhDeskPollingTimer = setInterval(async () => {
        if (currentPage === 'support') {
            await loadSupportConversations(false);
            if (cskhActiveSessionId) {
                await loadCskhConversationMessages(cskhActiveSessionId, false);
            }
        } else {
            stopCskhDeskPolling();
        }
    }, 3500);
}

function stopCskhDeskPolling() {
    if (cskhDeskPollingTimer) {
        clearInterval(cskhDeskPollingTimer);
        cskhDeskPollingTimer = null;
    }
}

function setCskhFilter(filter) {
    cskhFilter = filter;
    // Chỉ có 2 tab: waiting và my (đã bỏ tab all)
    ['waiting', 'my'].forEach(f => {
        const btn = document.getElementById('cskh-tab-' + f);
        if (btn) {
            if (f === filter) {
                btn.style.background = '#fff';
                btn.style.color = f === 'waiting' ? '#f59e0b' : '#10b981';
                btn.style.boxShadow = '0 1px 3px rgba(0,0,0,.08)';
            } else {
                btn.style.background = 'transparent';
                btn.style.color = '#64748b';
                btn.style.boxShadow = 'none';
            }
        }
    });
    filterCskhConversations();
}

async function loadSupportConversations(showSpinner = true) {
    const listEl = document.getElementById('cskh-conversations-list');
    if (showSpinner && listEl) listEl.innerHTML = '<div class="spinner"></div>';

    const { ok, data } = await api('GET', '/api/chat/staff/conversations');
    if (!ok || !data?.conversations) {
        if (listEl) listEl.innerHTML = '<div style="color:var(--danger);padding:20px;text-align:center">Lỗi tải danh sách hội thoại</div>';
        return;
    }

    cskhConversations = data.conversations;
    filterCskhConversations();
    if (cskhActiveSessionId) {
        activeCskhConversation = cskhConversations.find(c => c.sessionId === cskhActiveSessionId) || null;
        updateCskhActionButtons();
    }
}

function formatStaffDisplayName(fullName) {
    if (!fullName) return 'CSKH';
    const trimmed = fullName.trim();
    // 1. "NV CSKH 2 (Trần Mai)" -> "Trần Mai"
    const parenInside = trimmed.match(/\((.*?)\)/);
    if (parenInside && parenInside[1].trim()) {
        const val = parenInside[1].trim();
        if (!/^(CSKH|NV|Admin|\d+)/i.test(val)) {
            return val;
        }
    }
    // 2. "Trần Mai (CSKH 2)" -> "Trần Mai"
    const beforeParen = trimmed.match(/^(.*?)\s*\(/);
    if (beforeParen && beforeParen[1].trim()) {
        const val = beforeParen[1].trim();
        if (!/^NV\s+/i.test(val)) {
            return val;
        }
    }
    // 3. "NV Chăm Sóc Khách Hàng 1" -> "CSKH 1"
    if (/^NV\s+(Chăm\s+Sóc\s+Khách\s+Hàng|CSKH)/i.test(trimmed)) {
        const num = trimmed.match(/\d+/);
        return num ? `CSKH ${num[0]}` : 'CSKH';
    }
    return trimmed;
}

function renderCskhConversations(list) {
    const listEl = document.getElementById('cskh-conversations-list');
    if (!listEl) return;

    if (!list.length) {
        let emptyText = 'Chưa có cuộc trò chuyện nào';
        if (cskhFilter === 'waiting') emptyText = 'Không có khách nào đang chờ nhận 🎉';
        if (cskhFilter === 'my') emptyText = 'Bạn chưa tiếp nhận khách hàng nào';
        listEl.innerHTML = `<div style="text-align:center;padding:40px 10px;color:var(--text-3);font-size:13px">${emptyText}</div>`;
        return;
    }

    listEl.innerHTML = list.map(c => {
        const isActive = c.sessionId === cskhActiveSessionId;
        const timeStr = c.lastMessageAt ? new Date(c.lastMessageAt).toLocaleTimeString('vi-VN', {hour:'2-digit', minute:'2-digit'}) : '';
        const nameText = c.customerName || 'Khách hàng';
        const initial = (nameText.replace(/^Khách\s*#?/i, '').trim()[0] || 'K').toUpperCase();
        
        let senderPrefix = '';
        if (c.lastSender === 'staff') {
            senderPrefix = '<span style="color:#4f46e5;font-weight:600">Bạn: </span>';
        } else if (c.lastSender === 'bot') {
            senderPrefix = '<span style="color:#7c3aed;font-weight:600">AI: </span>';
        }

        // Status badge: Thiết kế pill hiện đại, gọn gàng, không bị tràn dòng
        let statusBadge = `
            <span class="cskh-status-pill pill-waiting" title="Chưa có nhân viên tiếp nhận">
                <span class="cskh-pill-dot"></span>
                <span>Chờ nhận</span>
            </span>`;
        if (c.status === 'in_progress') {
            if (c.assignedStaffId && c.assignedStaffId === authUser?.id) {
                statusBadge = `
                    <span class="cskh-status-pill pill-my" title="Bạn đang phụ trách khách hàng này">
                        <span class="cskh-pill-dot"></span>
                        <span>Bạn</span>
                    </span>`;
            } else {
                const cleanStaffName = formatStaffDisplayName(c.assignedStaffName);
                const words = cleanStaffName.split(/\s+/);
                const staffInitial = (words[words.length - 1][0] || words[0][0] || 'N').toUpperCase();
                statusBadge = `
                    <span class="cskh-status-pill pill-other" title="Nhân viên đang phụ trách: ${esc(c.assignedStaffName || cleanStaffName)}">
                        <span class="cskh-staff-avatar">${staffInitial}</span>
                        <span class="cskh-staff-name">${esc(cleanStaffName)}</span>
                    </span>`;
            }
        } else if (c.status === 'closed') {
            statusBadge = `
                <span class="cskh-status-pill pill-closed" title="Phiên hỗ trợ đã hoàn tất">
                    <span>Đã xong</span>
                </span>`;
        }

        return `
            <div class="cskh-conv-card ${isActive ? 'active' : ''}" onclick="selectCskhConversation('${esc(c.sessionId)}', '${esc(c.customerName)}', '${esc(c.customerPhone)}')">
                <div class="cskh-conv-avatar-wrap">
                    <div class="cskh-conv-avatar">${initial}</div>
                    <div class="cskh-conv-online-dot"></div>
                </div>
                <div class="cskh-conv-info">
                    <div class="cskh-conv-top">
                        <span class="cskh-conv-name">${esc(nameText)}</span>
                        <span class="cskh-conv-time">${timeStr}</span>
                    </div>
                    <div class="cskh-conv-bottom">
                        <div class="cskh-conv-msg">${senderPrefix}${esc(c.lastMessage || '–')}</div>
                        <div class="cskh-conv-tags">
                            ${statusBadge}
                            ${c.unreadCount > 0 ? `<span class="cskh-conv-badge">${c.unreadCount}</span>` : ''}
                        </div>
                    </div>
                </div>
            </div>`;
    }).join('');
}

function filterCskhConversations() {
    const q = document.getElementById('cskh-search-input')?.value.toLowerCase().trim() || '';
    let filtered = cskhConversations;

    // Lọc theo Tab CSKH (Chờ nhận, Của tôi)
    if (cskhFilter === 'waiting') {
        filtered = filtered.filter(c => c.status === 'waiting' || !c.assignedStaffId);
    } else if (cskhFilter === 'my') {
        filtered = filtered.filter(c => c.assignedStaffId && c.assignedStaffId === authUser?.id);
    }

    // Lọc theo từ khóa tìm kiếm
    if (q) {
        filtered = filtered.filter(c => 
            (c.customerName && c.customerName.toLowerCase().includes(q)) ||
            (c.lastMessage && c.lastMessage.toLowerCase().includes(q)) ||
            (c.customerPhone && c.customerPhone.includes(q)) ||
            (c.assignedStaffName && c.assignedStaffName.toLowerCase().includes(q))
        );
    }

    renderCskhConversations(filtered);
}

let activeCskhCustomerName = 'Khách hàng';

async function selectCskhConversation(sessionId, customerName, phone) {
    cskhActiveSessionId = sessionId;
    activeCskhCustomerName = customerName || 'Khách hàng';
    activeCskhConversation = cskhConversations.find(c => c.sessionId === sessionId) || null;
    filterCskhConversations();

    const initial = (activeCskhCustomerName.replace(/^Khách\s*#?/i, '').trim()[0] || 'K').toUpperCase();
    const avatarEl = document.getElementById('cskh-header-avatar');
    if (avatarEl) avatarEl.style.display = 'block';
    const avatarCircle = document.getElementById('cskh-header-avatar-circle');
    if (avatarCircle) avatarCircle.textContent = initial;

    const delBtn = document.getElementById('cskh-delete-btn');
    if (delBtn) delBtn.style.display = isAdmin() ? 'inline-block' : 'none';

    document.getElementById('cskh-active-customer-name').textContent = activeCskhCustomerName;
    document.getElementById('cskh-active-customer-info').innerHTML = '<span style="color:#22c55e;font-weight:600">🟢 Trực tuyến</span> • ' + (phone ? `SĐT: <strong>${phone}</strong> • ` : '') + `Phiên: #${sessionId.substring(0, 10)}`;
    
    // Cập nhật Banner & Các nút Hành Động Tiếp Nhận (Tránh 2-3 nhân viên trùng người)
    updateCskhActionButtons();

    document.getElementById('cskh-reply-form').style.display = 'flex';
    await loadCskhConversationMessages(sessionId, true);
    setTimeout(() => { document.getElementById('cskh-reply-input')?.focus(); }, 100);
}

function updateCskhActionButtons() {
    const banner = document.getElementById('cskh-assignment-banner');
    const claimBtn = document.getElementById('cskh-claim-btn');
    const transferBtn = document.getElementById('cskh-transfer-btn');
    const closeBtn = document.getElementById('cskh-close-btn');
    const replyInput = document.getElementById('cskh-reply-input');
    const replyBtn = document.getElementById('cskh-reply-btn');

    if (!activeCskhConversation) {
        if (banner) banner.style.display = 'none';
        if (claimBtn) claimBtn.style.display = 'none';
        if (transferBtn) transferBtn.style.display = 'none';
        if (closeBtn) closeBtn.style.display = 'none';
        return;
    }

    const conv = activeCskhConversation;
    const isWaiting = conv.status === 'waiting' || !conv.assignedStaffId;
    const isMyConversation = conv.assignedStaffId && conv.assignedStaffId === authUser?.id;
    const isOtherStaff = conv.assignedStaffId && conv.assignedStaffId !== authUser?.id;
    const isClosed = conv.status === 'closed';

    if (banner) banner.style.display = 'flex';

    if (isWaiting) {
        banner.style.background = '#fef3c7';
        banner.style.color = '#92400e';
        banner.innerHTML = '<span>🟡 <strong>Chưa có nhân viên tiếp nhận:</strong> Vui lòng bấm <em>"Tiếp nhận khách hàng"</em> để bắt đầu tư vấn.</span>';
        
        if (claimBtn) { 
            claimBtn.style.display = 'inline-flex'; 
            claimBtn.textContent = '✋ Tiếp nhận khách hàng'; 
            claimBtn.style.background = '#10b981'; 
        }
        if (transferBtn) transferBtn.style.display = 'none';
        if (closeBtn) closeBtn.style.display = 'none';
        if (replyInput) { 
            replyInput.disabled = true; 
            replyInput.placeholder = "Bấm '✋ Tiếp nhận khách hàng' phía trên để bắt đầu trả lời..."; 
        }
        if (replyBtn) replyBtn.disabled = true;
    } 
    else if (isClosed) {
        banner.style.background = '#f1f5f9';
        banner.style.color = '#475569';
        banner.innerHTML = '<span>⚪ <strong>Phiên hỗ trợ đã hoàn thành.</strong> Bạn có thể mở lại hỗ trợ nếu khách hàng cần giúp tiếp.</span>';
        
        if (claimBtn) { 
            claimBtn.style.display = 'inline-flex'; 
            claimBtn.textContent = '🔄 Tiếp nhận lại khách hàng'; 
            claimBtn.style.background = '#4f46e5'; 
        }
        if (transferBtn) transferBtn.style.display = 'none';
        if (closeBtn) closeBtn.style.display = 'none';
        if (replyInput) { 
            replyInput.disabled = true; 
            replyInput.placeholder = "Phiên hỗ trợ đã đóng..."; 
        }
        if (replyBtn) replyBtn.disabled = true;
    }
    else if (isMyConversation) {
        banner.style.background = '#ecfdf5';
        banner.style.color = '#065f46';
        banner.innerHTML = '<span>🟢 <strong>Bạn đang phụ trách khách hàng này.</strong> Toàn bộ lịch sử trao đổi được lưu giữ đầy đủ.</span>';
        
        if (claimBtn) claimBtn.style.display = 'none';
        if (transferBtn) transferBtn.style.display = 'inline-flex';
        if (closeBtn) closeBtn.style.display = 'inline-flex';
        if (replyInput) { 
            replyInput.disabled = false; 
            replyInput.placeholder = "Nhập câu trả lời tư vấn cho khách hàng..."; 
        }
        if (replyBtn) replyBtn.disabled = false;
    } 
    else if (isOtherStaff) {
        const cleanStaffName = formatStaffDisplayName(conv.assignedStaffName);
        banner.style.background = '#fff1f2';
        banner.style.color = '#9f1239';
        banner.style.borderBottom = '1px solid #fecdd3';
        banner.innerHTML = `<span style="display:flex;align-items:center;gap:6px">⚠️ Khách hàng đang được phụ trách bởi: <strong style="background:#ffe4e6;padding:2px 8px;border-radius:12px;color:#881337">👤 ${esc(cleanStaffName)}</strong>. Vui lòng không tư vấn trùng người!</span>`;
        
        if (claimBtn) { 
            claimBtn.style.display = 'inline-flex'; 
            claimBtn.textContent = '🔄 Nhận thay thế'; 
            claimBtn.style.background = '#ea580c'; 
        }
        if (transferBtn) transferBtn.style.display = 'none';
        if (closeBtn) closeBtn.style.display = 'none';
        if (replyInput) { 
            replyInput.disabled = true; 
            replyInput.placeholder = `Khách hàng do ${esc(cleanStaffName)} phụ trách (tránh trùng người)...`; 
        }
        if (replyBtn) replyBtn.disabled = true;
    }
}

async function claimActiveCskhConversation() {
    if (!cskhActiveSessionId) return;
    const { ok, data } = await api('POST', `/api/chat/staff/conversations/${encodeURIComponent(cskhActiveSessionId)}/claim`);
    if (ok) {
        toast(data.message || 'Đã tiếp nhận khách hàng thành công! 🎧', 'success');
        await loadSupportConversations(false);
        const updated = cskhConversations.find(c => c.sessionId === cskhActiveSessionId);
        if (updated) {
            activeCskhConversation = updated;
            updateCskhActionButtons();
            await loadCskhConversationMessages(cskhActiveSessionId, true);
        }
    } else {
        toast(data?.message || 'Lỗi khi tiếp nhận khách hàng', 'error');
    }
}

async function openCskhTransferModal() {
    if (!cskhActiveSessionId) return;
    const modal = document.getElementById('cskh-transfer-modal');
    const select = document.getElementById('cskh-transfer-target');
    const noteInput = document.getElementById('cskh-transfer-note');
    if (noteInput) noteInput.value = '';

    if (select) {
        select.innerHTML = '<option value="0">🟡 Trả về hàng đợi (Chờ nhân viên bất kỳ nhận)</option>';
        try {
            const { ok, data } = await api('GET', '/api/chat/staff/list');
            if (ok && Array.isArray(data?.list)) {
                data.list.forEach(s => {
                    if (s.id !== authUser?.id) {
                        const opt = document.createElement('option');
                        opt.value = s.id;
                        opt.textContent = `👤 ${s.fullName} (${s.username} - ${s.role === 'admin' ? 'Admin' : 'CSKH'})`;
                        select.appendChild(opt);
                    }
                });
            }
        } catch(e) {}
    }

    if (modal) modal.style.display = 'flex';
}

function closeCskhTransferModal() {
    const modal = document.getElementById('cskh-transfer-modal');
    if (modal) modal.style.display = 'none';
}

async function submitCskhTransfer() {
    if (!cskhActiveSessionId) return;
    const select = document.getElementById('cskh-transfer-target');
    const noteInput = document.getElementById('cskh-transfer-note');
    const targetVal = parseInt(select?.value || '0', 10);
    const targetStaffId = targetVal > 0 ? targetVal : null;
    const note = noteInput?.value?.trim() || '';

    const { ok, data } = await api('POST', `/api/chat/staff/conversations/${encodeURIComponent(cskhActiveSessionId)}/transfer`, {
        targetStaffId: targetStaffId,
        note: note
    });

    if (ok) {
        closeCskhTransferModal();
        toast(data?.message || 'Đã chuyển giao khách hàng thành công! Toàn bộ lịch sử chat được lưu giữ.', 'success');
        await loadSupportConversations(false);
        const updated = cskhConversations.find(c => c.sessionId === cskhActiveSessionId);
        if (updated) {
            activeCskhConversation = updated;
            updateCskhActionButtons();
            await loadCskhConversationMessages(cskhActiveSessionId, true);
        }
    } else {
        toast(data?.message || 'Lỗi khi chuyển giao khách hàng', 'error');
    }
}

async function closeActiveCskhConversation() {
    if (!cskhActiveSessionId) return;
    if (!confirm('Bạn có chắc đã tư vấn xong và muốn kết thúc phiên hỗ trợ này?')) return;
    const { ok, data } = await api('POST', `/api/chat/staff/conversations/${encodeURIComponent(cskhActiveSessionId)}/close`);
    if (ok) {
        toast('Đã hoàn thành và đóng phiên hỗ trợ!', 'success');
        await loadSupportConversations(false);
        const updated = cskhConversations.find(c => c.sessionId === cskhActiveSessionId);
        if (updated) {
            activeCskhConversation = updated;
            updateCskhActionButtons();
            await loadCskhConversationMessages(cskhActiveSessionId, true);
        }
    }
}

async function loadCskhConversationMessages(sessionId, scrollToBottom = true) {
    const msgEl = document.getElementById('cskh-chat-messages');
    const { ok, data } = await api('GET', `/api/chat/staff/conversations/${encodeURIComponent(sessionId)}`);
    if (!ok || !data?.messages) return;

    msgEl.innerHTML = data.messages.map(m => {
        const isStaff = m.sender === 'staff';
        const isBot = m.sender === 'bot';
        const senderCls = isStaff ? 'staff' : (isBot ? 'bot' : 'customer');
        const badgeIcon = isStaff ? '🎧' : (isBot ? '🤖' : '👤');
        const timeStr = m.createdAt ? new Date(m.createdAt).toLocaleTimeString('vi-VN', {hour:'2-digit', minute:'2-digit'}) : '';

        // Hiển thị thông báo chuyển giao khách hàng (nếu có)
        if (m.message && m.message.includes('🔄')) {
            return `
                <div style="text-align:center;margin:8px 0;">
                    <div style="display:inline-block;padding:6px 14px;background:#f1f5f9;color:#475569;border-radius:20px;font-size:12px;font-weight:600;border:1px dashed #cbd5e1;line-height:1.4">
                        ${esc(m.message)} • <span style="font-weight:400;color:#94a3b8">${timeStr}</span>
                    </div>
                </div>`;
        }

        let displayName = `Khách hàng: ${m.senderName || activeCskhCustomerName || 'Khách'}`;
        if (isStaff) {
            displayName = (m.senderName === authUser?.fullName || m.senderName === authUser?.username)
                ? 'Bạn (CSKH)'
                : `CSKH: ${formatStaffDisplayName(m.senderName)}`;
        } else if (isBot) {
            displayName = 'Trợ lý AI (Tư vấn tự động)';
        }

        return `
            <div class="chat-bubble-row ${senderCls}">
                <div class="chat-bubble-meta">
                    <span class="chat-avatar-pill">${badgeIcon} ${esc(displayName)}</span>
                    <span>${timeStr}</span>
                </div>
                <div class="chat-bubble">${formatChatMessage(m.message)}</div>
            </div>`;
    }).join('');

    if (scrollToBottom) msgEl.scrollTop = msgEl.scrollHeight;
}

async function deleteActiveCskhConversation() {
    if (!cskhActiveSessionId) return;
    if (!confirm(`Bạn có chắc muốn xóa vĩnh viễn cuộc trò chuyện này (#${cskhActiveSessionId.substring(0, 10)})?`)) return;

    const { ok, data } = await api('DELETE', `/api/chat/staff/conversations/${encodeURIComponent(cskhActiveSessionId)}`);
    if (ok) {
        toast('Đã xóa cuộc trò chuyện thành công!', 'success');
        cskhActiveSessionId = null;
        activeCskhConversation = null;
        document.getElementById('cskh-active-customer-name').textContent = 'Chọn một cuộc trò chuyện để bắt đầu';
        document.getElementById('cskh-active-customer-info').textContent = 'Vui lòng bấm vào khách hàng bên trái để xem tin nhắn';
        const avatarEl = document.getElementById('cskh-header-avatar');
        if (avatarEl) avatarEl.style.display = 'none';
        const delBtn = document.getElementById('cskh-delete-btn');
        if (delBtn) delBtn.style.display = 'none';
        const banner = document.getElementById('cskh-assignment-banner');
        if (banner) banner.style.display = 'none';
        document.getElementById('cskh-reply-form').style.display = 'none';
        document.getElementById('cskh-chat-messages').innerHTML = `
            <div style="text-align:center;padding:80px 20px;color:var(--text-3)">
                <div style="font-size:48px;margin-bottom:12px">💬</div>
                <div style="font-size:15px;font-weight:600">Chưa chọn hội thoại nào</div>
                <div style="font-size:13px;margin-top:4px">Vui lòng chọn khách hàng từ danh sách bên trái để phản hồi</div>
            </div>`;
        await loadSupportConversations();
    } else {
        toast('Lỗi khi xóa cuộc trò chuyện: ' + (data?.message || ''), 'error');
    }
}

async function handleCskhStaffReply(e) {
    e.preventDefault();
    if (!cskhActiveSessionId) return;

    const input = document.getElementById('cskh-reply-input');
    const msg = input.value.trim();
    if (!msg) return;

    input.value = '';
    const btn = document.getElementById('cskh-reply-btn');
    btn.disabled = true;

    const { ok, data } = await api('POST', '/api/chat/staff/reply', {
        sessionId: cskhActiveSessionId,
        message: msg
    });

    btn.disabled = false;

    if (ok) {
        await loadCskhConversationMessages(cskhActiveSessionId, true);
        await loadSupportConversations(false);
    } else {
        toast('Lỗi khi gửi phản hồi: ' + (data?.message || ''), 'error');
    }
}

/* INIT & STRICT ACTOR ROLE SEPARATION */
(async () => {
    updateNavAuth();
    initDraggableChat();
    updateChatWidgetForRole(); // Cập nhật widget ngay khi tải trang theo role hiện tại
    // Tải thương hiệu, danh mục và tỉnh/thành phố song song ở nền, không chặn hiển thị trang chính
    loadBrandsAndCategories();
    loadVnProvinces();
    
    const hash = window.location.hash.replace('#', '');
    const path = window.location.pathname.toLowerCase();
    
    if (path.includes('/admin')) {
        currentPage = 'admin';
        if (!isAdmin()) {
            toast('Bạn không có quyền truy cập trang Quản trị viên (Chỉ dành cho Admin)!', 'error');
            if (authUser?.role === 'support') window.location.href = '/support';
            else if (authUser?.role === 'warehouse') window.location.href = '/warehouse';
            else window.location.href = '/';
            return;
        }
        const el = document.getElementById('page-admin');
        if (el) el.style.display = 'block';
        const footer = document.getElementById('main-footer');
        if (footer) footer.style.display = 'none';
        loadAdminDashboard();
    } else if (path.includes('/support')) {
        currentPage = 'support';
        const isSupport = authUser?.role === 'support' || authUser?.role === 'admin';
        if (!isSupport) {
            toast('Bạn không có quyền truy cập Bàn làm việc CSKH (Chỉ dành cho CSKH / Admin)!', 'error');
            window.location.href = '/';
            return;
        }
        document.documentElement.classList.add('cskh-active');
        document.body.classList.add('cskh-active');
        const floatChat = document.getElementById('floating-chat-container');
        if (floatChat) floatChat.style.display = 'none';
        const el = document.getElementById('page-support');
        if (el) el.style.display = 'block';
        const footer = document.getElementById('main-footer');
        if (footer) footer.style.display = 'none';
        loadSupportDesk();
    } else if (path.includes('/warehouse')) {
        currentPage = 'warehouse';
        const isWarehouse = authUser?.role === 'warehouse' || authUser?.role === 'admin';
        if (!isWarehouse) {
            toast('Bạn không có quyền truy cập Kho hàng (Chỉ dành cho Thủ kho / Admin)!', 'error');
            window.location.href = '/';
            return;
        }
        const el = document.getElementById('page-warehouse');
        if (el) el.style.display = 'block';
        const footer = document.getElementById('main-footer');
        if (footer) footer.style.display = 'none';
        loadWarehouseInventory();
    } else if (path.includes('/orders')) {
        currentPage = 'orders';
        const el = document.getElementById('page-orders');
        if (el) el.style.display = 'block';
        if (!isLoggedIn()) {
            toast('Vui lòng đăng nhập để xem đơn mua', 'info');
            openAuthModal();
        } else {
            loadOrders();
        }
    } else if (path.startsWith('/product/')) {
        currentPage = 'product-detail';
        const catBar = document.getElementById('cat-bar');
        if (catBar) catBar.style.display = 'none';
    } else if (path.includes('/cart')) {
        currentPage = 'cart';
        const catBar = document.getElementById('cat-bar');
        if (catBar) catBar.style.display = 'none';
        if (isLoggedIn()) {
            await syncCart();
        }
    } else if (hash && ['orders', 'admin', 'warehouse', 'support'].includes(hash)) {
        showPage(hash, false);
    } else {
        const homeEl = document.getElementById('page-home');
        if (homeEl) {
            currentPage = 'home';
            const urlParams = new URLSearchParams(window.location.search);
            const catParam = urlParams.get('cat') || urlParams.get('category');
            const brandParam = urlParams.get('brand');
            const searchParam = urlParams.get('search') || urlParams.get('q');
            const sortParam = urlParams.get('sort');

            if (catParam) {
                activeCategory = catParam;
                document.querySelectorAll('.cat-pill').forEach(p => p.classList.remove('active'));
                const pill = document.querySelector(`.cat-pill[data-slug="${catParam}"]`);
                if (pill) pill.classList.add('active');
            }
            if (brandParam) {
                const brandSelect = document.getElementById('brand-select');
                if (brandSelect) {
                    brandSelect.value = brandParam;
                }
            }
            if (searchParam) {
                const searchInput = document.getElementById('search-input');
                if (searchInput) {
                    searchInput.value = searchParam;
                }
            }
            if (sortParam) {
                const sortSelect = document.getElementById('sort-select');
                if (sortSelect) {
                    sortSelect.value = sortParam;
                }
            }

            await loadProducts();

            if (catParam || brandParam || searchParam) {
                setTimeout(() => {
                    const target = document.getElementById('products-section');
                    if (target) {
                        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                    }
                }, 200);
            }
        }
    }

    if (isLoggedIn()) {
        await syncCart();
    }
})();