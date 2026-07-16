// Compact symbol catalog and command-completion data. These are insertion
// snippets, not a document/template gallery: every item is inserted at the
// current caret and the ◊ marker indicates the new caret position.

export function mathfieldInsertion(template) {
  const marker = template.indexOf('◊');
  if (marker < 0) return { latex: template, selectionMode: 'after' };

  // MathLive replaces #0 with the current visual selection, or with an
  // editable placeholder when the selection is collapsed. Empty argument
  // groups after the caret become follow-up placeholders for Tab navigation.
  return {
    latex: `${template.slice(0, marker)}#0${template.slice(marker + 1).replaceAll('{}', '{#?}')}`,
    selectionMode: 'placeholder',
  };
}

export const quickGroups = [
  {
    id: 'common', icon: '±', zh: '常用', en: 'Common',
    items: [
      ['x^{◊}', 'xⁿ', '上标', 'Power'], ['x_{◊}', 'xₙ', '下标', 'Subscript'], ['\\frac{◊}{}', 'ᵃ⁄ᵦ', '分数', 'Fraction'],
      ['\\sqrt{◊}', '√x', '平方根', 'Square root'], ['\\sqrt[◊]{}', 'ⁿ√x', 'n 次方根', 'Nth root'], ['\\left|◊\\right|', '|x|', '绝对值', 'Absolute value'],
      ['\\binom{◊}{}', '(ⁿₖ)', '二项式系数', 'Binomial'], ['\\text{◊}', 'abc', '文本', 'Text'], ['\\infty', '∞', '无穷', 'Infinity'],
      ['\\pm', '±', '正负号', 'Plus or minus'], ['\\cdot', '·', '点乘', 'Dot product'], ['\\times', '×', '乘号', 'Times'],
    ],
  },
  {
    id: 'greek', icon: 'α', zh: '希腊', en: 'Greek',
    items: [
      ['\\alpha', 'α', '阿尔法', 'alpha'], ['\\beta', 'β', '贝塔', 'beta'], ['\\gamma', 'γ', '伽马', 'gamma'], ['\\delta', 'δ', '德尔塔', 'delta'],
      ['\\epsilon', 'ε', '艾普西龙', 'epsilon'], ['\\varepsilon', 'ϵ', '变体艾普西龙', 'variant epsilon'], ['\\zeta', 'ζ', '泽塔', 'zeta'], ['\\eta', 'η', '伊塔', 'eta'],
      ['\\theta', 'θ', '西塔', 'theta'], ['\\vartheta', 'ϑ', '变体西塔', 'variant theta'], ['\\iota', 'ι', '约塔', 'iota'], ['\\kappa', 'κ', '卡帕', 'kappa'],
      ['\\lambda', 'λ', '兰姆达', 'lambda'], ['\\mu', 'μ', '缪', 'mu'], ['\\nu', 'ν', '纽', 'nu'], ['\\xi', 'ξ', '克西', 'xi'],
      ['\\pi', 'π', '派', 'pi'], ['\\varpi', 'ϖ', '变体派', 'variant pi'], ['\\rho', 'ρ', '柔', 'rho'], ['\\sigma', 'σ', '西格玛', 'sigma'],
      ['\\tau', 'τ', '陶', 'tau'], ['\\upsilon', 'υ', '宇普西龙', 'upsilon'], ['\\phi', 'φ', '斐', 'phi'], ['\\varphi', 'ϕ', '变体斐', 'variant phi'],
      ['\\chi', 'χ', '希', 'chi'], ['\\psi', 'ψ', '普西', 'psi'], ['\\omega', 'ω', '欧米伽', 'omega'], ['\\Gamma', 'Γ', '大写伽马', 'capital Gamma'],
      ['\\Delta', 'Δ', '大写德尔塔', 'capital Delta'], ['\\Theta', 'Θ', '大写西塔', 'capital Theta'], ['\\Lambda', 'Λ', '大写兰姆达', 'capital Lambda'], ['\\Sigma', 'Σ', '大写西格玛', 'capital Sigma'],
      ['\\Phi', 'Φ', '大写斐', 'capital Phi'], ['\\Psi', 'Ψ', '大写普西', 'capital Psi'], ['\\Omega', 'Ω', '大写欧米伽', 'capital Omega'],
    ],
  },
  {
    id: 'calculus', icon: '∫', zh: '微积分', en: 'Calculus',
    items: [
      ['\\lim_{x \\to ◊}', 'lim', '极限', 'Limit'], ['\\frac{d}{dx}◊', 'd/dx', '导数', 'Derivative'], ['\\frac{\\partial ◊}{\\partial x}', '∂/∂x', '偏导数', 'Partial derivative'],
      ['\\int ◊\\,dx', '∫', '积分', 'Integral'], ['\\int_{}^{} ◊\\,dx', '∫ₐᵇ', '定积分', 'Definite integral'], ['\\iint ◊\\,dA', '∬', '二重积分', 'Double integral'],
      ['\\iiint ◊\\,dV', '∭', '三重积分', 'Triple integral'], ['\\oint ◊\\,ds', '∮', '曲线积分', 'Contour integral'], ['\\nabla', '∇', '梯度', 'Nabla'],
      ['\\nabla \\cdot ◊', '∇·', '散度', 'Divergence'], ['\\nabla \\times ◊', '∇×', '旋度', 'Curl'], ['\\left.◊\\right|_{}^{}', '|ₐᵇ', '边界求值', 'Evaluate at bounds'],
    ],
  },
  {
    id: 'operators', icon: 'Σ', zh: '运算', en: 'Operators',
    items: [
      ['\\sum_{i=1}^{n} ◊', 'Σ', '求和', 'Sum'], ['\\prod_{i=1}^{n} ◊', 'Π', '连乘', 'Product'], ['\\coprod_{i=1}^{n} ◊', '∐', '余积', 'Coproduct'],
      ['\\bigcup_{i=1}^{n} ◊', '⋃', '大并集', 'Large union'], ['\\bigcap_{i=1}^{n} ◊', '⋂', '大交集', 'Large intersection'], ['\\bigoplus_{i=1}^{n} ◊', '⨁', '直和', 'Direct sum'],
      ['\\pm', '±', '正负', 'Plus or minus'], ['\\mp', '∓', '负正', 'Minus or plus'], ['\\times', '×', '乘', 'Times'], ['\\div', '÷', '除', 'Divide'],
      ['\\cdot', '·', '点乘', 'Dot'], ['\\ast', '∗', '星号', 'Asterisk'], ['\\star', '⋆', '星形', 'Star'], ['\\circ', '∘', '复合', 'Composition'],
      ['\\bullet', '•', '实心圆', 'Bullet'], ['\\oplus', '⊕', '圈加', 'Circled plus'], ['\\ominus', '⊖', '圈减', 'Circled minus'], ['\\otimes', '⊗', '张量积', 'Tensor product'],
      ['\\oslash', '⊘', '圈除', 'Circled slash'], ['\\odot', '⊙', '圈点', 'Circled dot'], ['\\wedge', '∧', '楔积', 'Wedge'], ['\\vee', '∨', '析取', 'Vee'],
    ],
  },
  {
    id: 'relations', icon: '≤', zh: '关系', en: 'Relations',
    items: [
      ['\\lt', '<', '小于', 'Less than'], ['\\gt', '>', '大于', 'Greater than'], ['\\leq', '≤', '小于等于', 'Less than or equal'], ['\\geq', '≥', '大于等于', 'Greater than or equal'],
      ['\\neq', '≠', '不等于', 'Not equal'], ['\\equiv', '≡', '恒等', 'Equivalent'], ['\\approx', '≈', '约等于', 'Approximately'], ['\\sim', '∼', '相似', 'Similar'],
      ['\\simeq', '≃', '渐近相等', 'Simeq'], ['\\cong', '≅', '全等', 'Congruent'], ['\\propto', '∝', '正比', 'Proportional'], ['\\ll', '≪', '远小于', 'Much less'],
      ['\\gg', '≫', '远大于', 'Much greater'], ['\\prec', '≺', '先于', 'Precedes'], ['\\succ', '≻', '后于', 'Succeeds'], ['\\preceq', '≼', '先于等于', 'Precedes or equal'],
      ['\\succeq', '≽', '后于等于', 'Succeeds or equal'], ['\\perp', '⊥', '垂直', 'Perpendicular'], ['\\parallel', '∥', '平行', 'Parallel'], ['\\mid', '∣', '整除', 'Divides'],
      ['\\nmid', '∤', '不整除', 'Does not divide'], ['\\models', '⊨', '满足', 'Models'], ['\\doteq', '≐', '定义相等', 'Doteq'], ['\\asymp', '≍', '渐近', 'Asymptotic'],
    ],
  },
  {
    id: 'arrows', icon: '→', zh: '箭头', en: 'Arrows',
    items: [
      ['\\leftarrow', '←', '左箭头', 'Left arrow'], ['\\rightarrow', '→', '右箭头', 'Right arrow'], ['\\leftrightarrow', '↔', '双向箭头', 'Left-right arrow'],
      ['\\Leftarrow', '⇐', '左双线箭头', 'Double left arrow'], ['\\Rightarrow', '⇒', '右双线箭头', 'Double right arrow'], ['\\Leftrightarrow', '⇔', '双向双线箭头', 'Double left-right arrow'],
      ['\\longleftarrow', '⟵', '长左箭头', 'Long left arrow'], ['\\longrightarrow', '⟶', '长右箭头', 'Long right arrow'], ['\\longleftrightarrow', '⟷', '长双向箭头', 'Long left-right arrow'],
      ['\\mapsto', '↦', '映射到', 'Maps to'], ['\\longmapsto', '⟼', '长映射箭头', 'Long maps to'], ['\\hookleftarrow', '↩', '左钩箭头', 'Hook left'],
      ['\\hookrightarrow', '↪', '右钩箭头', 'Hook right'], ['\\leftarrowtail', '↢', '左尾箭头', 'Left arrow tail'], ['\\rightarrowtail', '↣', '右尾箭头', 'Right arrow tail'],
      ['\\leftharpoonup', '↼', '左上鱼叉', 'Left harpoon up'], ['\\leftharpoondown', '↽', '左下鱼叉', 'Left harpoon down'], ['\\rightharpoonup', '⇀', '右上鱼叉', 'Right harpoon up'],
      ['\\rightharpoondown', '⇁', '右下鱼叉', 'Right harpoon down'], ['\\leftrightharpoons', '⇋', '左右鱼叉', 'Left-right harpoons'], ['\\rightleftharpoons', '⇌', '右左鱼叉', 'Right-left harpoons'],
      ['\\uparrow', '↑', '上箭头', 'Up arrow'], ['\\downarrow', '↓', '下箭头', 'Down arrow'], ['\\updownarrow', '↕', '上下箭头', 'Up-down arrow'],
      ['\\nearrow', '↗', '东北箭头', 'North-east arrow'], ['\\searrow', '↘', '东南箭头', 'South-east arrow'], ['\\swarrow', '↙', '西南箭头', 'South-west arrow'], ['\\nwarrow', '↖', '西北箭头', 'North-west arrow'],
    ],
  },
  {
    id: 'sets', icon: '∪', zh: '集合逻辑', en: 'Sets & logic',
    items: [
      ['\\in', '∈', '属于', 'Element of'], ['\\notin', '∉', '不属于', 'Not element of'], ['\\ni', '∋', '包含元素', 'Contains as member'], ['\\subset', '⊂', '真子集', 'Subset'],
      ['\\supset', '⊃', '真超集', 'Superset'], ['\\subseteq', '⊆', '子集', 'Subset or equal'], ['\\supseteq', '⊇', '超集', 'Superset or equal'], ['\\nsubseteq', '⊈', '非子集', 'Not subset'],
      ['\\cup', '∪', '并集', 'Union'], ['\\cap', '∩', '交集', 'Intersection'], ['\\setminus', '∖', '差集', 'Set difference'], ['\\emptyset', '∅', '空集', 'Empty set'],
      ['\\mathbb{N}', 'ℕ', '自然数集', 'Natural numbers'], ['\\mathbb{Z}', 'ℤ', '整数集', 'Integers'], ['\\mathbb{Q}', 'ℚ', '有理数集', 'Rationals'], ['\\mathbb{R}', 'ℝ', '实数集', 'Reals'],
      ['\\mathbb{C}', 'ℂ', '复数集', 'Complex numbers'], ['\\forall', '∀', '任意', 'For all'], ['\\exists', '∃', '存在', 'Exists'], ['\\nexists', '∄', '不存在', 'Does not exist'],
      ['\\neg', '¬', '非', 'Not'], ['\\land', '∧', '且', 'And'], ['\\lor', '∨', '或', 'Or'], ['\\therefore', '∴', '所以', 'Therefore'], ['\\because', '∵', '因为', 'Because'],
    ],
  },
  {
    id: 'delimiters', icon: '( )', zh: '括号定界', en: 'Delimiters',
    items: [
      ['\\left(◊\\right)', '( )', '自适应圆括号', 'Scalable parentheses'], ['\\left[◊\\right]', '[ ]', '自适应方括号', 'Scalable brackets'], ['\\left\\{◊\\right\\}', '{ }', '自适应花括号', 'Scalable braces'],
      ['\\left|◊\\right|', '| |', '绝对值', 'Absolute value'], ['\\left\\|◊\\right\\|', '‖ ‖', '范数', 'Norm'], ['\\left\\langle ◊ \\right\\rangle', '⟨ ⟩', '尖括号', 'Angle brackets'],
      ['\\left\\lfloor ◊ \\right\\rfloor', '⌊ ⌋', '向下取整', 'Floor'], ['\\left\\lceil ◊ \\right\\rceil', '⌈ ⌉', '向上取整', 'Ceiling'], ['\\underbrace{◊}_{ }', '⏟', '下花括号', 'Underbrace'],
      ['\\overbrace{◊}^{ }', '⏞', '上花括号', 'Overbrace'], ['\\left.◊\\right|', '. |', '单侧定界', 'One-sided delimiter'], ['\\left( ◊ \\middle| \\right)', '( | )', '中间定界符', 'Middle delimiter'],
    ],
  },
  {
    id: 'matrices', icon: '[⋮]', zh: '矩阵', en: 'Matrices',
    items: [
      ['\\begin{matrix}\n◊ & \\\\\n& \n\\end{matrix}', '⋮', '无括号矩阵', 'Matrix'],
      ['\\begin{pmatrix}\n◊ & \\\\\n& \n\\end{pmatrix}', '(⋮)', '圆括号矩阵', 'Parenthesized matrix'],
      ['\\begin{bmatrix}\n◊ & \\\\\n& \n\\end{bmatrix}', '[⋮]', '方括号矩阵', 'Bracketed matrix'],
      ['\\begin{Bmatrix}\n◊ & \\\\\n& \n\\end{Bmatrix}', '{⋮}', '花括号矩阵', 'Braced matrix'],
      ['\\begin{vmatrix}\n◊ & \\\\\n& \n\\end{vmatrix}', '|⋮|', '行列式', 'Determinant'],
      ['\\begin{Vmatrix}\n◊ & \\\\\n& \n\\end{Vmatrix}', '‖⋮‖', '双线矩阵', 'Double-bar matrix'],
      ['\\begin{cases}\n◊, & \\text{if } \\\\\n{}, & \\text{otherwise}\n\\end{cases}', '{…', '分段函数', 'Cases'],
      ['\\begin{aligned}\n◊ &= \\\\\n&= \n\\end{aligned}', '=…', '多行对齐', 'Aligned equations'],
    ],
  },
  {
    id: 'style', icon: '𝐀', zh: '字体标记', en: 'Style & accents',
    items: [
      ['\\mathbf{◊}', '𝐀', '粗体', 'Bold'], ['\\mathrm{◊}', 'A', '罗马正体', 'Roman'], ['\\mathit{◊}', '𝐴', '斜体', 'Italic'], ['\\mathsf{◊}', '𝖠', '无衬线', 'Sans serif'],
      ['\\mathtt{◊}', '𝙰', '等宽体', 'Monospace'], ['\\mathcal{◊}', '𝒜', '花体', 'Calligraphic'], ['\\mathbb{◊}', '𝔸', '黑板粗体', 'Blackboard bold'], ['\\mathfrak{◊}', '𝔄', '哥特体', 'Fraktur'],
      ['\\vec{◊}', 'x⃗', '向量箭头', 'Vector'], ['\\hat{◊}', 'x̂', '帽子', 'Hat'], ['\\widehat{◊}', 'x̂y', '宽帽子', 'Wide hat'], ['\\tilde{◊}', 'x̃', '波浪号', 'Tilde'],
      ['\\bar{◊}', 'x̄', '短上划线', 'Bar'], ['\\overline{◊}', 'xy̅', '上划线', 'Overline'], ['\\underline{◊}', 'x̲', '下划线', 'Underline'], ['\\dot{◊}', 'ẋ', '一点', 'Dot accent'],
      ['\\ddot{◊}', 'ẍ', '两点', 'Double-dot accent'], ['\\overrightarrow{◊}', '⟶', '上方右箭头', 'Over-arrow'],
    ],
  },
];

const simpleCommand = (name, symbol, zh, en) => [name, `\\${name}`, `\\${name}  ${symbol}`, zh, en];

const simpleCommandSpecs = [
  // Arrows are intentionally early so typing "\\lef" immediately offers the
  // useful arrow family before the generic scalable-delimiter command.
  ['leftarrow', '←', '左箭头', 'Left arrow'], ['leftrightarrow', '↔', '双向箭头', 'Left-right arrow'],
  ['leftarrowtail', '↢', '左尾箭头', 'Left arrow tail'], ['leftharpoonup', '↼', '左上鱼叉', 'Left harpoon up'],
  ['leftharpoondown', '↽', '左下鱼叉', 'Left harpoon down'], ['leftleftarrows', '⇇', '双左箭头', 'Paired left arrows'],
  ['leftrightharpoons', '⇋', '左右鱼叉', 'Left-right harpoons'], ['rightarrow', '→', '右箭头', 'Right arrow'],
  ['to', '→', '趋向/到', 'To'], ['rightleftarrows', '⇄', '右左双箭头', 'Right-left arrows'],
  ['rightleftharpoons', '⇌', '右左鱼叉', 'Right-left harpoons'], ['rightarrowtail', '↣', '右尾箭头', 'Right arrow tail'],
  ['rightharpoonup', '⇀', '右上鱼叉', 'Right harpoon up'], ['rightharpoondown', '⇁', '右下鱼叉', 'Right harpoon down'],
  ['Leftarrow', '⇐', '左双线箭头', 'Double left arrow'], ['Rightarrow', '⇒', '右双线箭头', 'Double right arrow'],
  ['Leftrightarrow', '⇔', '双向双线箭头', 'Double left-right arrow'], ['longleftarrow', '⟵', '长左箭头', 'Long left arrow'],
  ['longrightarrow', '⟶', '长右箭头', 'Long right arrow'], ['longleftrightarrow', '⟷', '长双向箭头', 'Long left-right arrow'],
  ['Longleftarrow', '⟸', '长左双线箭头', 'Long double left arrow'], ['Longrightarrow', '⟹', '长右双线箭头', 'Long double right arrow'],
  ['Longleftrightarrow', '⟺', '长双向双线箭头', 'Long double arrow'], ['mapsto', '↦', '映射到', 'Maps to'],
  ['longmapsto', '⟼', '长映射箭头', 'Long maps to'], ['hookleftarrow', '↩', '左钩箭头', 'Hook left'],
  ['hookrightarrow', '↪', '右钩箭头', 'Hook right'], ['uparrow', '↑', '上箭头', 'Up arrow'],
  ['downarrow', '↓', '下箭头', 'Down arrow'], ['updownarrow', '↕', '上下箭头', 'Up-down arrow'],
  ['Uparrow', '⇑', '上双线箭头', 'Double up arrow'], ['Downarrow', '⇓', '下双线箭头', 'Double down arrow'],
  ['Updownarrow', '⇕', '上下双线箭头', 'Double up-down arrow'], ['nearrow', '↗', '东北箭头', 'North-east arrow'],
  ['searrow', '↘', '东南箭头', 'South-east arrow'], ['swarrow', '↙', '西南箭头', 'South-west arrow'],
  ['nwarrow', '↖', '西北箭头', 'North-west arrow'], ['leadsto', '⇝', '引导到', 'Leads to'],

  // Relations and binary operations.
  ['leq', '≤', '小于等于', 'Less than or equal'], ['leqslant', '⩽', '斜小于等于', 'Slanted less or equal'],
  ['geq', '≥', '大于等于', 'Greater than or equal'], ['geqslant', '⩾', '斜大于等于', 'Slanted greater or equal'],
  ['neq', '≠', '不等于', 'Not equal'], ['equiv', '≡', '恒等', 'Equivalent'], ['approx', '≈', '约等于', 'Approximately'],
  ['sim', '∼', '相似', 'Similar'], ['simeq', '≃', '渐近相等', 'Simeq'], ['cong', '≅', '全等', 'Congruent'],
  ['propto', '∝', '正比', 'Proportional'], ['ll', '≪', '远小于', 'Much less'], ['gg', '≫', '远大于', 'Much greater'],
  ['lesssim', '≲', '小于或相似', 'Less or similar'], ['gtrsim', '≳', '大于或相似', 'Greater or similar'],
  ['prec', '≺', '先于', 'Precedes'], ['succ', '≻', '后于', 'Succeeds'], ['preceq', '≼', '先于等于', 'Precedes or equal'],
  ['succeq', '≽', '后于等于', 'Succeeds or equal'], ['perp', '⊥', '垂直', 'Perpendicular'], ['parallel', '∥', '平行', 'Parallel'],
  ['mid', '∣', '整除', 'Divides'], ['nmid', '∤', '不整除', 'Does not divide'], ['models', '⊨', '满足', 'Models'],
  ['doteq', '≐', '定义相等', 'Doteq'], ['asymp', '≍', '渐近', 'Asymptotic'],
  ['pm', '±', '正负号', 'Plus or minus'], ['mp', '∓', '负正号', 'Minus or plus'], ['times', '×', '乘号', 'Times'],
  ['div', '÷', '除号', 'Divide'], ['cdot', '·', '点乘', 'Dot'], ['ast', '∗', '星号', 'Asterisk'], ['star', '⋆', '星形', 'Star'],
  ['circ', '∘', '复合', 'Composition'], ['bullet', '•', '实心圆', 'Bullet'], ['oplus', '⊕', '圈加', 'Circled plus'],
  ['ominus', '⊖', '圈减', 'Circled minus'], ['otimes', '⊗', '张量积', 'Tensor product'], ['oslash', '⊘', '圈除', 'Circled slash'],
  ['odot', '⊙', '圈点', 'Circled dot'], ['diamond', '⋄', '菱形', 'Diamond'], ['wedge', '∧', '楔积/且', 'Wedge/and'],
  ['vee', '∨', '析取/或', 'Vee/or'], ['setminus', '∖', '集合差', 'Set difference'],

  // Greek letters.
  ['alpha', 'α', '希腊字母 α', 'Greek alpha'], ['beta', 'β', '希腊字母 β', 'Greek beta'],
  ['gamma', 'γ', '希腊字母 γ', 'Greek gamma'], ['delta', 'δ', '希腊字母 δ', 'Greek delta'],
  ['epsilon', 'ε', '希腊字母 ε', 'Greek epsilon'], ['varepsilon', 'ϵ', '变体 ε', 'Variant epsilon'],
  ['zeta', 'ζ', '希腊字母 ζ', 'Greek zeta'], ['eta', 'η', '希腊字母 η', 'Greek eta'],
  ['theta', 'θ', '希腊字母 θ', 'Greek theta'], ['vartheta', 'ϑ', '变体 θ', 'Variant theta'],
  ['iota', 'ι', '希腊字母 ι', 'Greek iota'], ['kappa', 'κ', '希腊字母 κ', 'Greek kappa'],
  ['varkappa', 'ϰ', '变体 κ', 'Variant kappa'], ['lambda', 'λ', '希腊字母 λ', 'Greek lambda'],
  ['mu', 'μ', '希腊字母 μ', 'Greek mu'], ['nu', 'ν', '希腊字母 ν', 'Greek nu'], ['xi', 'ξ', '希腊字母 ξ', 'Greek xi'],
  ['pi', 'π', '圆周率 π', 'Greek pi'], ['varpi', 'ϖ', '变体 π', 'Variant pi'], ['rho', 'ρ', '希腊字母 ρ', 'Greek rho'],
  ['varrho', 'ϱ', '变体 ρ', 'Variant rho'], ['sigma', 'σ', '希腊字母 σ', 'Greek sigma'], ['varsigma', 'ς', '词尾 σ', 'Final sigma'],
  ['tau', 'τ', '希腊字母 τ', 'Greek tau'], ['upsilon', 'υ', '希腊字母 υ', 'Greek upsilon'],
  ['phi', 'φ', '希腊字母 φ', 'Greek phi'], ['varphi', 'ϕ', '变体 φ', 'Variant phi'],
  ['chi', 'χ', '希腊字母 χ', 'Greek chi'], ['psi', 'ψ', '希腊字母 ψ', 'Greek psi'], ['omega', 'ω', '希腊字母 ω', 'Greek omega'],
  ['Gamma', 'Γ', '大写 Γ', 'Capital Gamma'], ['Delta', 'Δ', '大写 Δ', 'Capital Delta'], ['Theta', 'Θ', '大写 Θ', 'Capital Theta'],
  ['Lambda', 'Λ', '大写 Λ', 'Capital Lambda'], ['Xi', 'Ξ', '大写 Ξ', 'Capital Xi'], ['Pi', 'Π', '大写 Π', 'Capital Pi'],
  ['Sigma', 'Σ', '大写 Σ', 'Capital Sigma'], ['Upsilon', 'Υ', '大写 Υ', 'Capital Upsilon'], ['Phi', 'Φ', '大写 Φ', 'Capital Phi'],
  ['Psi', 'Ψ', '大写 Ψ', 'Capital Psi'], ['Omega', 'Ω', '大写 Ω', 'Capital Omega'],

  // Calculus, big operators and functions.
  ['partial', '∂', '偏导符号', 'Partial derivative'], ['nabla', '∇', '梯度符号', 'Nabla'], ['infty', '∞', '无穷', 'Infinity'],
  ['sum', 'Σ', '求和', 'Sum'], ['prod', 'Π', '连乘', 'Product'], ['coprod', '∐', '余积', 'Coproduct'],
  ['int', '∫', '积分', 'Integral'], ['iint', '∬', '二重积分', 'Double integral'], ['iiint', '∭', '三重积分', 'Triple integral'],
  ['oint', '∮', '曲线积分', 'Contour integral'], ['bigcup', '⋃', '大并集', 'Large union'], ['bigcap', '⋂', '大交集', 'Large intersection'],
  ['bigvee', '⋁', '大析取', 'Large vee'], ['bigwedge', '⋀', '大合取', 'Large wedge'], ['bigoplus', '⨁', '大圈加', 'Large direct sum'],
  ['bigotimes', '⨂', '大张量积', 'Large tensor product'], ['lim', 'lim', '极限', 'Limit'], ['limsup', 'lim sup', '上极限', 'Limit superior'],
  ['liminf', 'lim inf', '下极限', 'Limit inferior'], ['sup', 'sup', '上确界', 'Supremum'], ['inf', 'inf', '下确界', 'Infimum'],
  ['max', 'max', '最大值', 'Maximum'], ['min', 'min', '最小值', 'Minimum'], ['sin', 'sin', '正弦', 'Sine'], ['cos', 'cos', '余弦', 'Cosine'],
  ['tan', 'tan', '正切', 'Tangent'], ['cot', 'cot', '余切', 'Cotangent'], ['sec', 'sec', '正割', 'Secant'], ['csc', 'csc', '余割', 'Cosecant'],
  ['arcsin', 'arcsin', '反正弦', 'Arc sine'], ['arccos', 'arccos', '反余弦', 'Arc cosine'], ['arctan', 'arctan', '反正切', 'Arc tangent'],
  ['sinh', 'sinh', '双曲正弦', 'Hyperbolic sine'], ['cosh', 'cosh', '双曲余弦', 'Hyperbolic cosine'], ['tanh', 'tanh', '双曲正切', 'Hyperbolic tangent'],
  ['log', 'log', '对数', 'Logarithm'], ['ln', 'ln', '自然对数', 'Natural logarithm'], ['exp', 'exp', '指数函数', 'Exponential'],
  ['det', 'det', '行列式', 'Determinant'], ['gcd', 'gcd', '最大公因数', 'Greatest common divisor'], ['ker', 'ker', '核', 'Kernel'],
  ['dim', 'dim', '维数', 'Dimension'], ['Pr', 'Pr', '概率', 'Probability'],

  // Sets and logic.
  ['in', '∈', '属于', 'Element of'], ['notin', '∉', '不属于', 'Not element of'], ['ni', '∋', '包含元素', 'Contains as member'],
  ['subset', '⊂', '真子集', 'Subset'], ['supset', '⊃', '真超集', 'Superset'], ['subseteq', '⊆', '子集', 'Subset or equal'],
  ['supseteq', '⊇', '超集', 'Superset or equal'], ['nsubseteq', '⊈', '非子集', 'Not subset'], ['nsupseteq', '⊉', '非超集', 'Not superset'],
  ['cup', '∪', '并集', 'Union'], ['cap', '∩', '交集', 'Intersection'], ['emptyset', '∅', '空集', 'Empty set'],
  ['varnothing', '∅', '变体空集', 'Variant empty set'], ['forall', '∀', '任意', 'For all'], ['exists', '∃', '存在', 'Exists'],
  ['nexists', '∄', '不存在', 'Does not exist'], ['neg', '¬', '非', 'Not'], ['lnot', '¬', '逻辑非', 'Logical not'], ['land', '∧', '且', 'And'],
  ['lor', '∨', '或', 'Or'], ['therefore', '∴', '所以', 'Therefore'], ['because', '∵', '因为', 'Because'], ['top', '⊤', '真/顶', 'Top'],
  ['bot', '⊥', '假/底', 'Bottom'], ['vdash', '⊢', '推出', 'Proves'], ['dashv', '⊣', '反推出', 'Reverse turnstile'], ['vDash', '⊨', '语义蕴含', 'Semantic entailment'],

  // Delimiters, spacing and accents.
  ['langle', '⟨', '左尖括号', 'Left angle bracket'], ['rangle', '⟩', '右尖括号', 'Right angle bracket'],
  ['lceil', '⌈', '左上取整', 'Left ceiling'], ['rceil', '⌉', '右上取整', 'Right ceiling'],
  ['lfloor', '⌊', '左下取整', 'Left floor'], ['rfloor', '⌋', '右下取整', 'Right floor'],
  ['lvert', '|', '左竖线', 'Left vertical bar'], ['rvert', '|', '右竖线', 'Right vertical bar'],
  ['lVert', '‖', '左双竖线', 'Left double bar'], ['rVert', '‖', '右双竖线', 'Right double bar'],
  ['quad', ' ', '一倍字宽空格', 'Quad space'], ['qquad', '  ', '两倍字宽空格', 'Double quad space'],
];

export const latexCommands = [
  ['frac', '\\frac{◊}{}', '\\frac{a}{b}', '分数', 'Fraction'],
  ['dfrac', '\\dfrac{◊}{}', '\\dfrac{a}{b}', '行间分数', 'Display fraction'],
  ['tfrac', '\\tfrac{◊}{}', '\\tfrac{a}{b}', '行内分数', 'Text fraction'],
  ['cfrac', '\\cfrac{◊}{}', '\\cfrac{a}{b}', '连分数', 'Continued fraction'],
  ['sqrt', '\\sqrt{◊}', '\\sqrt{x}', '平方根', 'Square root'],
  ['sqrt', '\\sqrt[◊]{}', '\\sqrt[n]{x}', 'n 次方根', 'Nth root'],
  ['binom', '\\binom{◊}{}', '\\binom{n}{k}', '二项式系数', 'Binomial'],
  ['dbinom', '\\dbinom{◊}{}', '\\dbinom{n}{k}', '行间二项式系数', 'Display binomial'],
  ['overset', '\\overset{}{◊}', '\\overset{a}{b}', '在上方标注', 'Overset'],
  ['underset', '\\underset{}{◊}', '\\underset{a}{b}', '在下方标注', 'Underset'],
  ['substack', '\\substack{◊ \\\\ }', '\\substack{a \\\\ b}', '多行上下标', 'Multiline subscript'],
  ['text', '\\text{◊}', '\\text{text}', '公式内文本', 'Text in formula'],
  ['operatorname', '\\operatorname{◊}', '\\operatorname{rank}', '自定义算子', 'Named operator'],
  ['argmax', '\\operatorname*{arg\\,max}_{◊}', '\\operatorname*{arg\\,max}_{x}', '最大值参数', 'Arg max'],
  ['argmin', '\\operatorname*{arg\\,min}_{◊}', '\\operatorname*{arg\\,min}_{x}', '最小值参数', 'Arg min'],

  ...simpleCommandSpecs.map((spec) => simpleCommand(...spec)),

  ['left', '\\left(◊\\right)', '\\left( x \\right)', '自适应圆括号', 'Scalable parentheses'],
  ['leftbracket', '\\left[◊\\right]', '\\left[ x \\right]', '自适应方括号', 'Scalable brackets'],
  ['leftbrace', '\\left\\{◊\\right\\}', '\\left\\{ x \\right\\}', '自适应花括号', 'Scalable braces'],
  ['leftangle', '\\left\\langle ◊ \\right\\rangle', '\\left\\langle x \\right\\rangle', '自适应尖括号', 'Scalable angle brackets'],
  ['middle', '\\left( ◊ \\middle| \\right)', '\\middle|', '中间定界符', 'Middle delimiter'],
  ['underbrace', '\\underbrace{◊}_{ }', '\\underbrace{x}_{label}', '下花括号', 'Underbrace'],
  ['overbrace', '\\overbrace{◊}^{ }', '\\overbrace{x}^{label}', '上花括号', 'Overbrace'],
  ['vec', '\\vec{◊}', '\\vec{x}', '向量', 'Vector'], ['hat', '\\hat{◊}', '\\hat{x}', '上帽', 'Hat'],
  ['widehat', '\\widehat{◊}', '\\widehat{xyz}', '宽上帽', 'Wide hat'], ['tilde', '\\tilde{◊}', '\\tilde{x}', '波浪号', 'Tilde'],
  ['widetilde', '\\widetilde{◊}', '\\widetilde{xyz}', '宽波浪号', 'Wide tilde'], ['bar', '\\bar{◊}', '\\bar{x}', '短上划线', 'Bar'],
  ['overline', '\\overline{◊}', '\\overline{x}', '上划线', 'Overline'], ['underline', '\\underline{◊}', '\\underline{x}', '下划线', 'Underline'],
  ['overrightarrow', '\\overrightarrow{◊}', '\\overrightarrow{AB}', '上方右箭头', 'Arrow over'],
  ['overleftarrow', '\\overleftarrow{◊}', '\\overleftarrow{AB}', '上方左箭头', 'Left arrow over'],
  ['dot', '\\dot{◊}', '\\dot{x}', '一点', 'Dot accent'], ['ddot', '\\ddot{◊}', '\\ddot{x}', '两点', 'Double-dot accent'],
  ['mathbf', '\\mathbf{◊}', '\\mathbf{x}', '粗体', 'Bold'], ['mathrm', '\\mathrm{◊}', '\\mathrm{text}', '罗马正体', 'Roman'],
  ['mathit', '\\mathit{◊}', '\\mathit{x}', '数学斜体', 'Math italic'], ['mathsf', '\\mathsf{◊}', '\\mathsf{x}', '无衬线', 'Sans serif'],
  ['mathtt', '\\mathtt{◊}', '\\mathtt{x}', '等宽体', 'Monospace'], ['mathcal', '\\mathcal{◊}', '\\mathcal{F}', '花体', 'Calligraphic'],
  ['mathbb', '\\mathbb{◊}', '\\mathbb{R}', '黑板粗体', 'Blackboard bold'], ['mathfrak', '\\mathfrak{◊}', '\\mathfrak{g}', '哥特体', 'Fraktur'],
  ['boldsymbol', '\\boldsymbol{◊}', '\\boldsymbol{\\alpha}', '粗体符号', 'Bold symbol'],
  ['mathbbN', '\\mathbb{N}', '\\mathbb{N}', '自然数集', 'Natural numbers'], ['mathbbZ', '\\mathbb{Z}', '\\mathbb{Z}', '整数集', 'Integers'],
  ['mathbbQ', '\\mathbb{Q}', '\\mathbb{Q}', '有理数集', 'Rationals'], ['mathbbR', '\\mathbb{R}', '\\mathbb{R}', '实数集', 'Reals'],
  ['mathbbC', '\\mathbb{C}', '\\mathbb{C}', '复数集', 'Complex numbers'],

  ['begin', '\\begin{aligned}\n◊\n\\end{aligned}', 'aligned', '多行对齐', 'Aligned equations'],
  ['begin', '\\begin{cases}\n◊\n\\end{cases}', 'cases', '分段函数', 'Cases'],
  ['begin', '\\begin{matrix}\n◊\n\\end{matrix}', 'matrix', '矩阵', 'Matrix'],
  ['begin', '\\begin{pmatrix}\n◊\n\\end{pmatrix}', 'pmatrix', '圆括号矩阵', 'Parenthesized matrix'],
  ['begin', '\\begin{bmatrix}\n◊\n\\end{bmatrix}', 'bmatrix', '方括号矩阵', 'Bracketed matrix'],
  ['begin', '\\begin{Bmatrix}\n◊\n\\end{Bmatrix}', 'Bmatrix', '花括号矩阵', 'Braced matrix'],
  ['begin', '\\begin{vmatrix}\n◊\n\\end{vmatrix}', 'vmatrix', '行列式', 'Determinant matrix'],
  ['begin', '\\begin{Vmatrix}\n◊\n\\end{Vmatrix}', 'Vmatrix', '双线矩阵', 'Double-bar matrix'],
  ['begin', '\\begin{gathered}\n◊\n\\end{gathered}', 'gathered', '多行居中', 'Gathered equations'],
  ['begin', '\\begin{array}{cc}\n◊ & \\\\\n& \n\\end{array}', 'array', '数组', 'Array'],
  ['aligned', '\\begin{aligned}\n◊ &= \\\\\n&= \n\\end{aligned}', 'aligned', '多行对齐环境', 'Aligned environment'],
  ['cases', '\\begin{cases}\n◊, & \\text{if } \\\\\n{}, & \\text{otherwise}\n\\end{cases}', 'cases', '分段函数环境', 'Cases environment'],
  ['matrix', '\\begin{matrix}\n◊ & \\\\\n& \n\\end{matrix}', 'matrix', '无括号矩阵', 'Matrix environment'],
  ['pmatrix', '\\begin{pmatrix}\n◊ & \\\\\n& \n\\end{pmatrix}', 'pmatrix', '圆括号矩阵', 'Parenthesized matrix'],
  ['bmatrix', '\\begin{bmatrix}\n◊ & \\\\\n& \n\\end{bmatrix}', 'bmatrix', '方括号矩阵', 'Bracketed matrix'],
  ['Bmatrix', '\\begin{Bmatrix}\n◊ & \\\\\n& \n\\end{Bmatrix}', 'Bmatrix', '花括号矩阵', 'Braced matrix'],
  ['vmatrix', '\\begin{vmatrix}\n◊ & \\\\\n& \n\\end{vmatrix}', 'vmatrix', '行列式', 'Determinant matrix'],
  ['Vmatrix', '\\begin{Vmatrix}\n◊ & \\\\\n& \n\\end{Vmatrix}', 'Vmatrix', '双线矩阵', 'Double-bar matrix'],
  ['smallmatrix', '\\begin{smallmatrix}\n◊ & \\\\\n& \n\\end{smallmatrix}', 'smallmatrix', '行内小矩阵', 'Small matrix'],
];
