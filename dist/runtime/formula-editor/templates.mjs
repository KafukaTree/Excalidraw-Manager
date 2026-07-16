export const quickGroups = [
  {
    id: 'common', zh: '常用', en: 'Common',
    items: [
      ['x^{◊}', 'xⁿ', 'Power'], ['x_{◊}', 'xₙ', 'Subscript'], ['\\frac{◊}{}', 'a⁄b', 'Fraction'],
      ['\\sqrt{◊}', '√x', 'Square root'], ['\\sqrt[]{◊}', 'ⁿ√x', 'Nth root'], ['\\left|◊\\right|', '|x|', 'Absolute value'],
    ],
  },
  {
    id: 'greek', zh: '希腊字母', en: 'Greek',
    items: [
      ['\\alpha', 'α', 'alpha'], ['\\beta', 'β', 'beta'], ['\\gamma', 'γ', 'gamma'], ['\\delta', 'δ', 'delta'],
      ['\\theta', 'θ', 'theta'], ['\\lambda', 'λ', 'lambda'], ['\\mu', 'μ', 'mu'], ['\\pi', 'π', 'pi'],
      ['\\rho', 'ρ', 'rho'], ['\\sigma', 'σ', 'sigma'], ['\\phi', 'φ', 'phi'], ['\\omega', 'ω', 'omega'],
    ],
  },
  {
    id: 'calculus', zh: '微积分', en: 'Calculus',
    items: [
      ['\\lim_{x \\to ◊}', 'lim', 'Limit'], ['\\frac{d}{dx}◊', 'd/dx', 'Derivative'], ['\\partial_{◊}', '∂', 'Partial derivative'],
      ['\\int ◊\\,dx', '∫', 'Integral'], ['\\int_{}^{} ◊\\,dx', '∫ₐᵇ', 'Definite integral'], ['\\iint ◊\\,dA', '∬', 'Double integral'],
    ],
  },
  {
    id: 'large', zh: '大型运算', en: 'Operators',
    items: [
      ['\\sum_{i=1}^{n} ◊', 'Σ', 'Sum'], ['\\prod_{i=1}^{n} ◊', 'Π', 'Product'], ['\\bigcup_{i=1}^{n} ◊', '⋃', 'Union'],
      ['\\bigcap_{i=1}^{n} ◊', '⋂', 'Intersection'], ['\\max_{◊}', 'max', 'Maximum'], ['\\min_{◊}', 'min', 'Minimum'],
    ],
  },
  {
    id: 'relations', zh: '关系与集合', en: 'Relations',
    items: [
      ['\\leq', '≤', 'less than or equal'], ['\\geq', '≥', 'greater than or equal'], ['\\neq', '≠', 'not equal'],
      ['\\approx', '≈', 'approximately'], ['\\in', '∈', 'element of'], ['\\notin', '∉', 'not element of'],
      ['\\subseteq', '⊆', 'subset'], ['\\cup', '∪', 'union'], ['\\cap', '∩', 'intersection'], ['\\emptyset', '∅', 'empty set'],
    ],
  },
  {
    id: 'arrows', zh: '箭头', en: 'Arrows',
    items: [
      ['\\to', '→', 'right arrow'], ['\\leftarrow', '←', 'left arrow'], ['\\leftrightarrow', '↔', 'both directions'],
      ['\\Rightarrow', '⇒', 'implies'], ['\\Leftrightarrow', '⇔', 'if and only if'], ['\\mapsto', '↦', 'maps to'],
    ],
  },
];

export const templateCategories = [
  {
    id: 'algebra', zh: '代数', en: 'Algebra', templates: [
      ['二次公式', 'Quadratic formula', 'x=\\frac{-b\\pm\\sqrt{b^2-4ac}}{2a}'],
      ['二项式', 'Binomial theorem', '(x+y)^n=\\sum_{k=0}^{n}\\binom{n}{k}x^{n-k}y^k'],
      ['分段函数', 'Piecewise function', 'f(x)=\\begin{cases}x^2,&x\\geq0\\\\-x,&x<0\\end{cases}'],
      ['方程组', 'Equation system', '\\begin{aligned}ax+by&=c\\\\dx+ey&=f\\end{aligned}'],
    ],
  },
  {
    id: 'calculus', zh: '微积分', en: 'Calculus', templates: [
      ['极限定义', 'Limit definition', '\\lim_{x\\to a}f(x)=L'],
      ['定积分', 'Definite integral', '\\int_{a}^{b}f(x)\\,dx'],
      ['泰勒展开', 'Taylor series', 'f(x)=\\sum_{n=0}^{\\infty}\\frac{f^{(n)}(a)}{n!}(x-a)^n'],
      ['梯度', 'Gradient', '\\nabla f=\\left(\\frac{\\partial f}{\\partial x_1},\\ldots,\\frac{\\partial f}{\\partial x_n}\\right)'],
    ],
  },
  {
    id: 'linear', zh: '线性代数', en: 'Linear algebra', templates: [
      ['矩阵', 'Matrix', 'A=\\begin{bmatrix}a&b\\\\c&d\\end{bmatrix}'],
      ['行列式', 'Determinant', '\\det(A)=\\begin{vmatrix}a&b\\\\c&d\\end{vmatrix}=ad-bc'],
      ['特征方程', 'Eigen equation', 'A\\mathbf{v}=\\lambda\\mathbf{v}'],
      ['内积', 'Inner product', '\\langle\\mathbf{u},\\mathbf{v}\\rangle=\\sum_{i=1}^{n}u_iv_i'],
    ],
  },
  {
    id: 'probability', zh: '概率统计', en: 'Probability', templates: [
      ['正态分布', 'Normal distribution', 'f(x)=\\frac{1}{\\sigma\\sqrt{2\\pi}}e^{-\\frac{1}{2}\\left(\\frac{x-\\mu}{\\sigma}\\right)^2}'],
      ['期望', 'Expectation', '\\mathbb{E}[X]=\\sum_x x\\,P(X=x)'],
      ['方差', 'Variance', '\\operatorname{Var}(X)=\\mathbb{E}[(X-\\mu)^2]'],
      ['贝叶斯公式', 'Bayes theorem', 'P(A\\mid B)=\\frac{P(B\\mid A)P(A)}{P(B)}'],
    ],
  },
  {
    id: 'physics', zh: '物理', en: 'Physics', templates: [
      ['质能方程', 'Mass-energy', 'E=mc^2'],
      ['薛定谔方程', 'Schrödinger equation', 'i\\hbar\\frac{\\partial}{\\partial t}\\Psi=\\hat{H}\\Psi'],
      ['麦克斯韦方程', 'Maxwell equation', '\\nabla\\cdot\\mathbf{E}=\\frac{\\rho}{\\varepsilon_0}'],
      ['傅里叶变换', 'Fourier transform', '\\hat{f}(\\xi)=\\int_{-\\infty}^{\\infty}f(x)e^{-2\\pi i x\\xi}\\,dx'],
    ],
  },
];
